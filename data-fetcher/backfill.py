"""历史数据回填脚本（任务3）。

用 Tushare opt_daily 按 trade_date 拉取过去 60 个交易日的
50ETF 期权日线（仅 SSE），写入 options_daily 表，标的统一标 510050。

使用方式：
    python backfill.py                      # 默认回填过去 60 个交易日（截至 20260804）
    python backfill.py --end 20260804       # 指定结束日期
    python backfill.py --days 60 --end 20260804
"""
from __future__ import annotations

import argparse
import logging
from datetime import datetime, timedelta

import pandas as pd

from config import Config
from db.writer import DbWriter
from fetchers.tushare_fetcher import TushareFetcher, clean_dataframe

logger = logging.getLogger(__name__)

# 50ETF 期权标的（写入 options_daily.Underlying），固定不变
UNDERLYING = "510050"


def generate_candidate_dates(end_date: str, days: int) -> list[str]:
    """从 end_date（含）向前推 N 个自然日，作为候选拉取日期。

    非交易日（Tushare 返回空）会在主循环里被跳过，所以这里无需手工过滤周末。
    """
    end = datetime.strptime(end_date, "%Y%m%d")
    return [(end - timedelta(days=i)).strftime("%Y%m%d") for i in range(days)]


def fetch_sse_50etf_daily(fetcher: TushareFetcher, trade_date: str) -> list[dict]:
    """仅拉取 SSE 交易所的 50ETF 期权日线，标的统一打 510050。"""
    try:
        df = fetcher.pro.opt_daily(trade_date=trade_date, exchange="SSE")
    except Exception as exc:  # noqa: BLE001
        logger.error("opt_daily 拉取失败 %s: %s", trade_date, exc)
        return []
    if df is None or len(df) == 0:
        return []

    # 用 TushareFetcher 提供的清洗工具：NaN→None、日期转 date、numpy→原生类型
    records = clean_dataframe(df)
    for record in records:
        # 50ETF 期权都在 SSE，标的统一标 510050
        record["underlying"] = UNDERLYING
        record.setdefault("trade_date", trade_date)
    return records


def backfill(end_date: str, days: int) -> None:
    config = Config.load()
    fetcher = TushareFetcher(config.tushare_token)
    db_writer = DbWriter(
        host=config.db_host,
        port=config.db_port,
        dbname=config.db_name,
        user=config.db_user,
        password=config.db_password,
    )

    candidates = generate_candidate_dates(end_date, days)
    logger.info("候选日期范围: %s → %s, 共 %d 个自然日",
                candidates[-1], candidates[0], len(candidates))

    total_rows = 0
    total_days_with_data = 0
    total_records = []

    for trade_date in candidates:
        records = fetch_sse_50etf_daily(fetcher, trade_date)
        if not records:
            logger.info("%s 无数据（非交易日或休市）", trade_date)
            continue
        total_records.extend(records)
        total_days_with_data += 1
        logger.info("%s 拉取 %d 条合约", trade_date, len(records))

    if not total_records:
        logger.warning("回填结束：未拉到任何数据")
        return

    # 用 pandas 统一再清洗一次（去 NaN、统一 trade_date 字符串格式）
    df_all = pd.DataFrame(total_records)
    df_all = df_all.replace({pd.NA: None, float("nan"): None})
    df_all["trade_date"] = pd.to_datetime(
        df_all["trade_date"], format="%Y%m%d", errors="coerce"
    ).dt.date

    # 转回 records 喂给 DbWriter（DbWriter 内部仍会做一次 _to_date/_to_decimal 保护）
    final_records = []
    for r in df_all.to_dict("records"):
        clean_r = {}
        for k, v in r.items():
            if v is None:
                clean_r[k] = None
            elif hasattr(v, "isoformat") and not isinstance(v, str):
                clean_r[k] = v.isoformat()
            else:
                clean_r[k] = v
        final_records.append(clean_r)

    written = db_writer.write_option_daily(final_records)
    total_rows = written
    logger.info(
        "回填完成: 候选 %d 天 -> 命中 %d 个交易日 -> 共 %d 行 options_daily",
        len(candidates), total_days_with_data, total_rows,
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="VolSurf 50ETF 期权历史回填")
    parser.add_argument(
        "--end", default="20260804",
        help="结束日期 YYYYMMDD（默认 20260804，从这一天往前推 N 天）",
    )
    parser.add_argument(
        "--days", type=int, default=90,
        help="向前推的自然日数（默认 90，足以覆盖 60 个交易日，遇到非交易日会跳过）",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    backfill(end_date=args.end, days=args.days)
