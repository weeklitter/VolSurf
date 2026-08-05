"""Fetch 510300 ETF daily + 000300 (CFFEX) option contracts/daily + index daily.

This script handles:
  1. 510300 ETF underlying_daily (backfill to match options_daily date range)
  2. 000300 CFFEX option contracts (opt_basic exchange='CFFEX')
  3. 000300 CFFEX option daily (opt_daily exchange='CFFEX')
  4. 000300 index daily (000300.SH via Tushare daily)

Usage:
    python3 fetch_510300_000300.py
"""
from __future__ import annotations

import logging
import sys
import time
from datetime import datetime, timedelta
from decimal import Decimal

import pandas as pd
import tushare as ts

from config import Config
from db.writer import DbWriter, _to_date, _to_decimal

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger(__name__)

TUSHARE_TOKEN = "__TUSHARE_TOKEN__"


def generate_date_range(start_date: str, end_date: str) -> list[str]:
    """Generate list of YYYYMMDD strings from start to end (inclusive)."""
    start = datetime.strptime(start_date, "%Y%m%d")
    end = datetime.strptime(end_date, "%Y%m%d")
    dates = []
    current = start
    while current <= end:
        dates.append(current.strftime("%Y%m%d"))
        current += timedelta(days=1)
    return dates


def fetch_510300_etf_daily(pro, db_writer: DbWriter) -> int:
    """Fetch 510300 ETF daily prices from Tushare and write to underlying_daily."""
    logger.info("=== Step 1: Fetching 510300 ETF daily (underlying_daily) ===")

    # 510300 is an ETF listed on SSE. Use Tushare fund_daily or daily.
    # Try fund_daily first (for ETFs), fallback to daily
    try:
        df = pro.fund_daily(ts_code="510300.SH", start_date="20260401", end_date="20260804")
        if df is None or len(df) == 0:
            logger.warning("fund_daily returned empty for 510300.SH, trying daily...")
            df = pro.daily(ts_code="510300.SH", start_date="20260401", end_date="20260804")
    except Exception as exc:
        logger.warning(f"fund_daily failed: {exc}, trying daily...")
        df = pro.daily(ts_code="510300.SH", start_date="20260401", end_date="20260804")

    if df is None or len(df) == 0:
        logger.error("Failed to fetch 510300 ETF daily data from Tushare")
        return 0

    logger.info(f"Got {len(df)} rows for 510300.SH")
    count = 0
    for _, row in df.iterrows():
        trade_date = str(row.get("trade_date", ""))
        close = row.get("close")
        if close is None or pd.isna(close):
            continue
        try:
            db_writer.write_underlying_daily("510300", trade_date, float(close))
            count += 1
        except Exception as exc:
            logger.error(f"Failed to write 510300 daily for {trade_date}: {exc}")

    logger.info(f"510300 ETF daily: wrote {count} rows to underlying_daily")
    return count


def fetch_000300_contracts(pro, db_writer: DbWriter) -> int:
    """Fetch 000300 CFFEX option contracts via opt_basic."""
    logger.info("=== Step 2: Fetching 000300 CFFEX option contracts ===")

    try:
        df = pro.opt_basic(exchange="CFFEX")
    except Exception as exc:
        logger.error(f"opt_basic CFFEX failed: {exc}")
        return 0

    if df is None or len(df) == 0:
        logger.error("opt_basic returned empty for CFFEX")
        return 0

    logger.info(f"Got {len(df)} CFFEX option contracts from Tushare")

    # Clean and prepare records
    import numpy as np
    df = df.replace({np.nan: None})

    # Convert date fields
    for date_col in ("maturity_date", "list_date", "delist_date"):
        if date_col in df.columns:
            df[date_col] = pd.to_datetime(
                df[date_col], format="%Y%m%d", errors="coerce"
            ).dt.date

    records = df.to_dict("records")
    cleaned = []
    for row in records:
        clean_r = {}
        for k, v in row.items():
            if isinstance(v, np.integer):
                clean_r[k] = int(v)
            elif isinstance(v, np.floating):
                clean_r[k] = float(v) if not np.isnan(v) else None
            elif isinstance(v, np.bool_):
                clean_r[k] = bool(v)
            elif isinstance(v, pd.Timestamp):
                clean_r[k] = v.to_pydatetime().date() if not pd.isna(v) else None
            else:
                clean_r[k] = v
        # Set underlying to 000300 for all CFFEX contracts
        clean_r["underlying"] = "000300"
        cleaned.append(clean_r)

    written = db_writer.upsert_option_contracts(cleaned)
    logger.info(f"000300 CFFEX contracts: wrote {written} rows to options_contracts")
    return written


def fetch_000300_option_daily(pro, db_writer: DbWriter, start_date: str, end_date: str) -> int:
    """Fetch 000300 CFFEX option daily data via opt_daily."""
    logger.info(f"=== Step 3: Fetching 000300 CFFEX option daily ({start_date} to {end_date}) ===")

    dates = generate_date_range(start_date, end_date)
    logger.info(f"Generated {len(dates)} candidate dates")

    total_written = 0
    days_with_data = 0

    # Collect all records, then batch write
    all_records = []

    for trade_date in dates:
        try:
            df = pro.opt_daily(trade_date=trade_date, exchange="CFFEX")
        except Exception as exc:
            logger.debug(f"opt_daily CFFEX {trade_date} failed: {exc}")
            continue

        if df is None or len(df) == 0:
            continue

        days_with_data += 1
        # Clean the data
        import numpy as np
        df = df.replace({np.nan: None})

        # Convert date fields
        for date_col in ("trade_date",):
            if date_col in df.columns:
                df[date_col] = pd.to_datetime(
                    df[date_col], format="%Y%m%d", errors="coerce"
                ).dt.date

        records = df.to_dict("records")
        for row in records:
            clean_r = {}
            for k, v in row.items():
                if isinstance(v, np.integer):
                    clean_r[k] = int(v)
                elif isinstance(v, np.floating):
                    clean_r[k] = float(v) if not np.isnan(v) else None
                elif isinstance(v, np.bool_):
                    clean_r[k] = bool(v)
                elif isinstance(v, pd.Timestamp):
                    clean_r[k] = v.to_pydatetime().date() if not pd.isna(v) else None
                else:
                    clean_r[k] = v
            clean_r["underlying"] = "000300"
            all_records.append(clean_r)

        if days_with_data % 10 == 0:
            logger.info(f"  Progress: {days_with_data} trading days, {len(all_records)} records so far")

        # Tushare rate limit: ~200 calls/min for 2000 points
        time.sleep(0.4)

    logger.info(f"Fetched {len(all_records)} records from {days_with_data} trading days")

    if all_records:
        # Batch write in chunks of 5000
        chunk_size = 5000
        for i in range(0, len(all_records), chunk_size):
            chunk = all_records[i:i + chunk_size]
            written = db_writer.write_option_daily(chunk)
            total_written += written
            logger.info(f"  Wrote chunk {i//chunk_size + 1}: {written} rows")

    logger.info(f"000300 CFFEX option daily: wrote {total_written} rows to options_daily")
    return total_written


def fetch_000300_index_daily(pro, db_writer: DbWriter, start_date: str, end_date: str) -> int:
    """Fetch 000300 (CSI 300 index) daily data via Tushare index_daily."""
    logger.info(f"=== Step 4: Fetching 000300 index daily ({start_date} to {end_date}) ===")

    # 000300.SH is the CSI 300 index
    # Use index_daily for index data
    try:
        df = pro.index_daily(ts_code="000300.SH", start_date=start_date, end_date=end_date)
    except Exception as exc:
        logger.error(f"index_daily 000300.SH failed: {exc}")
        # Try daily as fallback
        try:
            df = pro.daily(ts_code="000300.SH", start_date=start_date, end_date=end_date)
        except Exception as exc2:
            logger.error(f"daily 000300.SH also failed: {exc2}")
            return 0

    if df is None or len(df) == 0:
        logger.error("Failed to fetch 000300 index daily data")
        return 0

    logger.info(f"Got {len(df)} rows for 000300.SH index")
    count = 0
    for _, row in df.iterrows():
        trade_date = str(row.get("trade_date", ""))
        close = row.get("close")
        if close is None or pd.isna(close):
            continue
        try:
            db_writer.write_underlying_daily("000300", trade_date, float(close))
            count += 1
        except Exception as exc:
            logger.error(f"Failed to write 000300 daily for {trade_date}: {exc}")

    logger.info(f"000300 index daily: wrote {count} rows to underlying_daily")
    return count


def main():
    logger.info("Starting data fetch for 510300 ETF daily + 000300 CFFEX options")

    # Set up Tushare
    ts.set_token(TUSHARE_TOKEN)
    pro = ts.pro_api(TUSHARE_TOKEN)

    # Set up DB writer (connect to localhost since we're running outside Docker)
    db_writer = DbWriter(
        host="localhost",
        port=5432,
        dbname="volsurf",
        user="volsurf",
        password="__DB_PASSWORD__",
    )

    # Date range: match the 510050/510300 options_daily range (2026-05-07 to 2026-08-04)
    # Extend slightly to cover underlying_daily which starts earlier (2026-04-01 for 510050)
    start_date = "20260401"
    end_date = "20260804"

    # Step 1: 510300 ETF daily
    count_510300 = fetch_510300_etf_daily(pro, db_writer)

    # Step 2: 000300 CFFEX contracts
    count_contracts = fetch_000300_contracts(pro, db_writer)

    # Step 3: 000300 CFFEX option daily
    count_daily = fetch_000300_option_daily(pro, db_writer, start_date, end_date)

    # Step 4: 000300 index daily
    count_index = fetch_000300_index_daily(pro, db_writer, start_date, end_date)

    # Summary
    logger.info("=" * 60)
    logger.info("SUMMARY:")
    logger.info(f"  510300 ETF daily (underlying_daily): {count_510300} rows")
    logger.info(f"  000300 CFFEX contracts:              {count_contracts} rows")
    logger.info(f"  000300 CFFEX option daily:           {count_daily} rows")
    logger.info(f"  000300 index daily (underlying_daily): {count_index} rows")
    logger.info("=" * 60)


if __name__ == "__main__":
    main()
