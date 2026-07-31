"""APScheduler 定时任务：每个交易日 17:30 自动跑日终数据更新。"""
from __future__ import annotations

import asyncio
import logging
from datetime import datetime
from typing import Optional

from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.triggers.cron import CronTrigger

from db.writer import DbWriter
from fetchers.base_fetcher import BaseFetcher
from notify.api_notifier import ApiNotifier

logger = logging.getLogger(__name__)


class DailyUpdateScheduler:
    """协调 fetcher → db_writer → api_notifier 三件套。"""

    # MVP 阶段支持的标的
    UNDERLYINGS = ("510050", "510300", "000300")
    EXCHANGES = ("SSE", "SZSE", "CFFEX")

    def __init__(
        self,
        fetcher: BaseFetcher,
        db_writer: DbWriter,
        api_notifier: ApiNotifier,
        hour: int = 17,
        minute: int = 30,
    ):
        self.fetcher = fetcher
        self.db_writer = db_writer
        self.api_notifier = api_notifier
        self.scheduler = AsyncIOScheduler(timezone="Asia/Shanghai")
        self.hour = hour
        self.minute = minute

    # ── 调度 ──────────────────────────────────────────────────────────────
    def start(self) -> None:
        self.scheduler.add_job(
            self.run_daily_update,
            CronTrigger(hour=self.hour, minute=self.minute),
            id="daily_update",
            misfire_grace_time=3600,
            replace_existing=True,
            coalesce=True,
        )
        self.scheduler.start()
        logger.info(
            "Scheduler started: daily_update at %02d:%02d",
            self.hour, self.minute,
        )

    def stop(self) -> None:
        if self.scheduler.running:
            self.scheduler.shutdown(wait=False)
            logger.info("Scheduler stopped")

    # ── 手动触发 ──────────────────────────────────────────────────────────
    async def run_now(self, trade_date: Optional[str] = None) -> None:
        if trade_date is None:
            trade_date = datetime.now().strftime("%Y%m%d")
        await self.run_daily_update(trade_date=trade_date)

    # ── 主流程 ────────────────────────────────────────────────────────────
    async def run_daily_update(self, trade_date: Optional[str] = None) -> None:
        if trade_date is None:
            trade_date = datetime.now().strftime("%Y%m%d")

        max_retries = 3
        retry_delay = 600  # 10 分钟

        last_exc: Exception | None = None
        for attempt in range(1, max_retries + 1):
            try:
                logger.info(
                    "Daily update attempt %d/%d for %s", attempt, max_retries, trade_date
                )

                # 1. 新鲜度校验
                if not self.fetcher.check_data_freshness(trade_date):
                    logger.warning("数据未就绪 %s，%ds 后重试", trade_date, retry_delay)
                    if attempt < max_retries:
                        await asyncio.sleep(retry_delay)
                    continue

                failed_steps: list[str] = []

                # 2. 期权日线
                try:
                    option_data = self.fetcher.fetch_option_daily(trade_date)
                    self.db_writer.write_option_daily(option_data)
                    logger.info("Step 2 OK: 期权日线 %d 条", len(option_data))
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 2 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("fetch_option_daily")

                # 3. 合约信息
                try:
                    for exchange in self.EXCHANGES:
                        contracts = self.fetcher.fetch_option_basic(exchange)
                        self.db_writer.upsert_option_contracts(contracts)
                    logger.info("Step 3 OK: 合约信息已更新")
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 3 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("fetch_option_basic")

                # 4. 标的价格
                try:
                    for ul in self.UNDERLYINGS:
                        price = self.fetcher.fetch_underlying_daily(ul, trade_date)
                        if price and price.get("close") is not None:
                            self.db_writer.write_underlying_daily(
                                ul, price["trade_date"], float(price["close"])
                            )
                    logger.info("Step 4 OK: 标的价格已更新")
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 4 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("fetch_underlying_daily")

                # 5. 通知 .NET 计算（带重试）
                try:
                    result = await self.api_notifier.trigger_calc(trade_date, max_retries=3)
                    logger.info("Step 5 OK: 触发计算 %s", result)
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 5 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("trigger_calc")

                if failed_steps:
                    logger.warning("完成但有失败步骤: %s", failed_steps)
                    await self.notify_failure(trade_date, failed_steps=failed_steps)
                else:
                    logger.info("Daily update 全部成功: %s", trade_date)
                return

            except Exception as exc:  # noqa: BLE001
                last_exc = exc
                logger.error("Daily update 顶层失败: %s", exc, exc_info=True)
                if attempt < max_retries:
                    await asyncio.sleep(retry_delay)

        logger.critical("Daily update 失败 %d 次: %s", max_retries, trade_date)
        await self.notify_failure(trade_date, last_error=str(last_exc) if last_exc else None)

    # ── 告警（MVP 占位） ──────────────────────────────────────────────────
    async def notify_failure(
        self,
        trade_date: str,
        failed_steps: Optional[list[str]] = None,
        last_error: Optional[str] = None,
    ) -> None:
        """发送告警（企业微信/钉钉/邮件 webhook）。

        MVP: 仅写日志，后续可在此实现 webhook。
        """
        logger.error(
            "ALERT: VolSurf daily update failed date=%s steps=%s error=%s",
            trade_date, failed_steps, last_error,
        )
        # TODO: 接入 webhook 通知