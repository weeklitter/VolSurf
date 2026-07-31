"""历史数据回填脚本。

按日期范围逐日拉取 Tushare 期权 / 合约 / 标的数据并入库，
分批触发 .NET 回算 IV（避免一次性把 252 天塞进 Channel）。

使用方式：
    python backfill.py --start 20250801 --end 20260731
"""
from __future__ import annotations

import argparse
import asyncio
import logging
from datetime import datetime, timedelta

from config import Config
from db.writer import DbWriter
from fetchers.tushare_fetcher import TushareFetcher
from notify.api_notifier import ApiNotifier

logger = logging.getLogger(__name__)

UNDERLYINGS = ("510050", "510300", "000300")
EXCHANGES = ("SSE", "SZSE", "CFFEX")
RECALC_BATCH_DAYS = 50  # 每批回算触发一次


async def backfill(start_date: str, end_date: str) -> None:
    config = Config.load()
    fetcher = TushareFetcher(config.tushare_token)
    db_writer = DbWriter(
        host=config.db_host,
        port=config.db_port,
        dbname=config.db_name,
        user=config.db_user,
        password=config.db_password,
    )
    api_notifier = ApiNotifier(config.api_base_url, config.internal_key)

    start = datetime.strptime(start_date, "%Y%m%d")
    end = datetime.strptime(end_date, "%Y%m%d")
    total_days = (end - start).days + 1
    processed = 0

    batch_start = start
    current = start

    while current <= end:
        trade_date = current.strftime("%Y%m%d")
        processed += 1
        logger.info("Backfill %s (%d/%d)", trade_date, processed, total_days)

        try:
            # 1. 期权日线
            option_data = fetcher.fetch_option_daily(trade_date)
            if option_data:
                db_writer.write_option_daily(option_data)

            # 2. 合约信息
            for exchange in EXCHANGES:
                contracts = fetcher.fetch_option_basic(exchange)
                if contracts:
                    db_writer.upsert_option_contracts(contracts)

            # 3. 标的价格
            for ul in UNDERLYINGS:
                price = fetcher.fetch_underlying_daily(ul, trade_date)
                if price and price.get("close") is not None:
                    db_writer.write_underlying_daily(
                        ul, price["trade_date"], float(price["close"])
                    )

            # 4. 每 N 天批量触发 .NET 回算
            days_done = (current - batch_start).days + 1
            if days_done % RECALC_BATCH_DAYS == 0 or current == end:
                logger.info("批量触发 IV 回算 %s → %s", batch_start.strftime("%Y%m%d"), trade_date)
                await _batch_recalc(api_notifier, batch_start, current)
                batch_start = current + timedelta(days=1)

            # 5. 限速：Tushare 每分钟最多 200 次
            await asyncio.sleep(0.5)

        except Exception as exc:  # noqa: BLE001
            logger.error("Backfill failed %s: %s", trade_date, exc, exc_info=True)

        current += timedelta(days=1)

    logger.info("Backfill 完成: %d 天", processed)


async def _batch_recalc(api_notifier: ApiNotifier, start: datetime, end: datetime) -> None:
    """批量触发指定日期范围的 IV 回算。"""
    current = start
    while current <= end:
        trade_date = current.strftime("%Y%m%d")
        try:
            await api_notifier.trigger_calc(trade_date, max_retries=3)
            await asyncio.sleep(2)  # 限速
        except Exception as exc:  # noqa: BLE001
            logger.error("Recalc trigger failed %s: %s", trade_date, exc)
        current += timedelta(days=1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="VolSurf 历史数据回填")
    parser.add_argument("--start", required=True, help="起始日期 YYYYMMDD")
    parser.add_argument("--end", required=True, help="结束日期 YYYYMMDD")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    asyncio.run(backfill(args.start, args.end))