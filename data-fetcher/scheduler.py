"""APScheduler 定时任务：每个交易日 17:30 自动跑日终数据更新。

每日自动流程（run_daily_update）：
  1. 新鲜度校验（ETL 数据中心已发布今日数据）
  2. Tushare 拉取期权日线行情 → upsert options_daily
  3. AKShare 拉取上交所当日期权合约 → upsert options_contracts
  4. 拉取标的价格（ETF: AKShare fund_etf_hist_sina; 沪深300指数: Tushare daily）
     → upsert underlying_daily
  5. POST 通知 .NET API 触发 IV/Greeks 计算（api:5000/api/internal/trigger-calc）

失败重试：整个流程失败（含新鲜度未通过）3 次，间隔 10 分钟。
3 次都失败 → logger.critical 写告警（MVP：仅日志；TODO 接 webhook）。
"""
from __future__ import annotations

import asyncio
import logging
from datetime import datetime, timedelta
from typing import Optional

from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.triggers.cron import CronTrigger

from db.writer import DbWriter
from fetchers.akshare_fetcher import AkshareFetcher
from fetchers.base_fetcher import BaseFetcher
from fetchers.tushare_fetcher import TushareFetcher
from notify.api_notifier import ApiNotifier

logger = logging.getLogger(__name__)


class DailyUpdateScheduler:
    """协调 fetcher → db_writer → api_notifier 三件套。"""

    # MVP 阶段支持的标的
    UNDERLYINGS = ("510050", "510300", "000300")

    def __init__(
        self,
        fetcher: BaseFetcher,
        db_writer: DbWriter,
        api_notifier: ApiNotifier,
        akshare_fetcher: Optional[AkshareFetcher] = None,
        hour: int = 17,
        minute: int = 30,
    ):
        self.fetcher = fetcher
        self.db_writer = db_writer
        self.api_notifier = api_notifier
        # AKShare 拉合约：若 caller 没传，懒加载一个（install 可能没装）
        self.akshare_fetcher = akshare_fetcher or AkshareFetcher()
        self.scheduler = AsyncIOScheduler(timezone="Asia/Shanghai")
        self.hour = hour
        self.minute = minute

    # ── 调度 ──────────────────────────────────────────────────────────────
    def start(self) -> None:
        """注册 17:30 每日任务并启动调度器（Asia/Shanghai）。

        misfire_grace_time=3600s：服务重启后 1h 内的"应跑未跑"任务仍会补跑一次。
        coalesce=True：错过的多次任务合并成一次，避免堆积。
        """
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
            "Scheduler started: daily_update at %02d:%02d Asia/Shanghai",
            self.hour, self.minute,
        )

    def stop(self) -> None:
        if self.scheduler.running:
            self.scheduler.shutdown(wait=False)
            logger.info("Scheduler stopped")

    # ── 手动触发 ──────────────────────────────────────────────────────────
    async def run_now(self, trade_date: Optional[str] = None) -> None:
        """手动立即跑一次（用于 FastAPI /trigger/manual 或运维触发）。"""
        if trade_date is None:
            trade_date = datetime.now().strftime("%Y%m%d")
        await self.run_daily_update(trade_date=trade_date)

    # ── 主流程 ────────────────────────────────────────────────────────────
    async def run_daily_update(
        self,
        trade_date: Optional[str] = None,
        max_retries: int = 3,
        retry_delay_seconds: int = 600,  # 10 分钟
    ) -> None:
        """完整日终流程。

        Args:
            trade_date: 交易日期（YYYYMMDD 字符串），None 取今日。
            max_retries: 整体失败重试次数。
            retry_delay_seconds: 重试间隔秒数。
        """
        if trade_date is None:
            trade_date = datetime.now().strftime("%Y%m%d")

        # 校验交易日期格式
        try:
            datetime.strptime(trade_date, "%Y%m%d")
        except ValueError:
            logger.error("run_daily_update: trade_date 格式非法 %r", trade_date)
            return

        last_exc: Exception | None = None
        for attempt in range(1, max_retries + 1):
            try:
                logger.info(
                    "Daily update attempt %d/%d for %s", attempt, max_retries, trade_date
                )

                # ── Step 1: 新鲜度校验 ──────────────────────────────
                if not self.fetcher.check_data_freshness(trade_date):
                    logger.warning(
                        "Step 1 数据未就绪 %s，%ds 后重试",
                        trade_date, retry_delay_seconds,
                    )
                    if attempt < max_retries:
                        await asyncio.sleep(retry_delay_seconds)
                    continue

                failed_steps: list[str] = []

                # ── Step 2: Tushare → 期权日线 ───────────────────────
                try:
                    option_data = self.fetcher.fetch_option_daily(trade_date)
                    if option_data:
                        self.db_writer.write_option_daily(option_data)
                        logger.info(
                            "Step 2 OK: 期权日线 %d 条", len(option_data)
                        )
                    else:
                        logger.warning("Step 2: Tushare 未返回期权日线数据")
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 2 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("fetch_option_daily")

                # ── Step 3: AKShare → 上交所合约信息 ──────────────────
                try:
                    if not getattr(self.akshare_fetcher, "_enabled", False):
                        logger.warning(
                            "Step 3 跳过：akshare 不可用，请安装 akshare"
                        )
                    else:
                        contracts = self.akshare_fetcher.fetch_option_contracts(
                            exchange="SSE"
                        )
                        if contracts:
                            self.db_writer.upsert_option_contracts(contracts)
                            logger.info(
                                "Step 3 OK: 合约信息 %d 条", len(contracts)
                            )
                        else:
                            logger.warning("Step 3: AKShare 未返回合约数据")
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 3 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("fetch_option_contracts")

                # ── Step 4: 标的价格 ─────────────────────────────────
                # 50ETF/300ETF → AKShare fund_etf_hist_sina
                # 沪深300指数 → Tushare daily（已由 fetcher 内部按 ts_code 分流）
                try:
                    for ul in self.UNDERLYINGS:
                        price = self.fetcher.fetch_underlying_daily(ul, trade_date)
                        if price and price.get("close") is not None:
                            # 标的日线写入用 YYYYMMDD
                            td_str = (
                                price["trade_date"]
                                if isinstance(price["trade_date"], str)
                                and len(price["trade_date"]) == 8
                                else trade_date
                            )
                            self.db_writer.write_underlying_daily(
                                ul, td_str, float(price["close"])
                            )
                    logger.info("Step 4 OK: 标的价格已更新")
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 4 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("fetch_underlying_daily")

                # ── Step 5: 通知 .NET 计算 ───────────────────────────
                try:
                    result = await self.api_notifier.trigger_calc(
                        trade_date, max_retries=3
                    )
                    logger.info("Step 5 OK: 触发计算 %s", result)
                except Exception as exc:  # noqa: BLE001
                    logger.error("Step 5 FAILED: %s", exc, exc_info=True)
                    failed_steps.append("trigger_calc")

                if failed_steps:
                    logger.warning(
                        "完成但有失败步骤 %s: %s",
                        trade_date, failed_steps,
                    )
                    await self.notify_failure(
                        trade_date, failed_steps=failed_steps
                    )
                else:
                    logger.info(
                        "Daily update 全部成功: %s (%.1fs)",
                        trade_date,
                        0.0,  # 占位；如需精确耗时可加 perf_counter
                    )
                return

            except Exception as exc:  # noqa: BLE001
                last_exc = exc
                logger.error(
                    "Daily update 顶层失败 attempt=%d: %s",
                    attempt, exc, exc_info=True,
                )
                if attempt < max_retries:
                    await asyncio.sleep(retry_delay_seconds)

        logger.critical(
            "Daily update 失败 %d 次: %s last_exc=%s",
            max_retries, trade_date, last_exc,
        )
        await self.notify_failure(
            trade_date,
            failed_steps=None,
            last_error=str(last_exc) if last_exc else None,
        )

    # ── 告警（MVP 占位） ──────────────────────────────────────────────────
    async def notify_failure(
        self,
        trade_date: str,
        failed_steps: Optional[list[str]] = None,
        last_error: Optional[str] = None,
    ) -> None:
        """发送告警（企业微信/钉钉/邮件 webhook）。

        MVP: 仅写 logger.error 级日志，后续可在此实现 webhook。
        """
        logger.error(
            "ALERT: VolSurf daily update failed date=%s steps=%s error=%s",
            trade_date, failed_steps, last_error,
        )
        # TODO: 接入 webhook 通知
