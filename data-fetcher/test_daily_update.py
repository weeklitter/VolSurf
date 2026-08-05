"""手动触发一次日终完整流程。

直接执行：
    cd data-fetcher && python3 test_daily_update.py [--date YYYYMMDD]

不依赖 FastAPI / APScheduler；直接调用 DailyUpdateScheduler.run_daily_update。
可选 `--skip-notify` 跳过最末的 .NET 通知步骤（仅做 ETL 写入测试）。
"""
from __future__ import annotations

import argparse
import asyncio
import logging
import os
import sys
from datetime import datetime

from config import Config
from db.writer import DbWriter
from fetchers.akshare_fetcher import AkshareFetcher
from fetchers.tushare_fetcher import TushareFetcher
from notify.api_notifier import ApiNotifier
from scheduler import DailyUpdateScheduler


def main() -> int:
    parser = argparse.ArgumentParser(
        description="VolSurf 日终更新手动触发脚本"
    )
    parser.add_argument(
        "--date", default=None,
        help="交易日期 YYYYMMDD；缺省取今日",
    )
    parser.add_argument(
        "--skip-notify", action="store_true",
        help="跳过最后一步 .NET trigger-calc",
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true",
        help="DEBUG 级日志",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    log = logging.getLogger("test_daily_update")

    # ── 配置加载 ──
    cfg = Config.load()
    # 手动触发脚本默认在容器外运行，DB HOST 改用 localhost；
    # 也可通过环境变量 LOCAL_DB_HOST 覆盖（如远程开发库）。
    db_host = os.environ.get("LOCAL_DB_HOST", "localhost")
    cfg = Config(
        tushare_token=cfg.tushare_token,
        db_host=db_host,
        db_port=cfg.db_port,
        db_name=cfg.db_name,
        db_user=cfg.db_user,
        db_password=cfg.db_password,
        api_base_url=cfg.api_base_url,
        internal_key=cfg.internal_key,
        log_level=cfg.log_level,
    )

    if not cfg.tushare_token:
        log.error("TUSHARE_TOKEN 未配置，无法继续")
        return 1
    if not cfg.internal_key and not args.skip_notify:
        log.warning("INTERNAL_KEY 未配置，Step 5 trigger-calc 会 401")

    trade_date = args.date or datetime.now().strftime("%Y%m%d")
    if len(trade_date) != 8 or not trade_date.isdigit():
        log.error("trade_date 格式非法: %r", trade_date)
        return 2

    # ── 装配 fetcher / writer / notifier / scheduler ──
    fetcher = TushareFetcher(cfg.tushare_token)
    akshare_fetcher = AkshareFetcher()
    db_writer = DbWriter(
        host=cfg.db_host, port=cfg.db_port,
        dbname=cfg.db_name, user=cfg.db_user, password=cfg.db_password,
    )
    api_notifier = ApiNotifier(cfg.api_base_url, cfg.internal_key)

    scheduler = DailyUpdateScheduler(
        fetcher=fetcher,
        db_writer=db_writer,
        api_notifier=api_notifier,
        akshare_fetcher=akshare_fetcher,
    )

    if args.skip_notify:
        # 单独 patch api_notifier.trigger_calc -> 立即成功，避免触发 .NET 调用
        async def _noop(_trade_date: str, **_kw):
            log.info("[skip-notify] 跳过 .NET trigger-calc")
            return {"code": 200, "data": {"status": "skipped"}}

        api_notifier.trigger_calc = _noop  # type: ignore[assignment]

    # ── 执行完整流程 ──
    log.info("开始执行日终更新: trade_date=%s", trade_date)
    try:
        asyncio.run(scheduler.run_daily_update(trade_date=trade_date))
    except KeyboardInterrupt:
        log.warning("用户中断")
        return 130
    except Exception as exc:  # noqa: BLE001
        log.error("日终更新异常退出: %s", exc, exc_info=True)
        return 99

    # ── 验证写入 ──
    log.info("=" * 60)
    log.info("数据验证")
    try:
        import psycopg2

        conn = psycopg2.connect(
            host=cfg.db_host, port=cfg.db_port,
            dbname=cfg.db_name, user=cfg.db_user, password=cfg.db_password,
        )
        with conn, conn.cursor() as cur:
            cur.execute(
                "SELECT \"Underlying\", COUNT(*) FROM options_daily "
                "WHERE \"TradeDate\" = %s GROUP BY \"Underlying\"",
                (datetime.strptime(trade_date, "%Y%m%d").date(),),
            )
            log.info("options_daily (%s): %s", trade_date, cur.fetchall())

            cur.execute(
                "SELECT COUNT(*) FROM options_contracts"
            )
            total_c = cur.fetchone()[0]
            log.info("options_contracts 总条数: %d", total_c)

            cur.execute(
                "SELECT \"TsCode\", \"Close\" FROM underlying_daily "
                "WHERE \"TradeDate\" = %s ORDER BY \"TsCode\"",
                (datetime.strptime(trade_date, "%Y%m%d").date(),),
            )
            log.info("underlying_daily (%s): %s", trade_date, cur.fetchall())
    except Exception as exc:  # noqa: BLE001
        log.error("验证查询失败: %s", exc, exc_info=True)

    log.info("=" * 60)
    log.info("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
