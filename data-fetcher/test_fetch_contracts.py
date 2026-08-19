"""测试：使用 AKShare 拉取上交所当日期权合约信息并写入 PostgreSQL。

直接执行：
    cd data-fetcher && python3 test_fetch_contracts.py

成功后查询数据库：
    PGPASSWORD=$DB_PASSWORD psql -h localhost -U volsurf -d volsurf \
      -c 'SELECT COUNT(*), MIN("MaturityDate"), MAX("MaturityDate") FROM options_contracts;'
"""
from __future__ import annotations

import logging
import os
import sys

from fetchers.akshare_fetcher import AkshareFetcher
from db.writer import DbWriter


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )
    log = logging.getLogger("test_fetch_contracts")

    fetcher = AkshareFetcher()
    if not getattr(fetcher, "_enabled", False):
        log.error("akshare 不可用，请先安装: pip3 install akshare")
        return 1

    log.info("Step 1: 调用 ak.option_current_day_sse() 拉取上交所合约清单")
    contracts = fetcher.fetch_option_contracts(exchange="SSE")
    if not contracts:
        log.error("未拉取到任何合约，退出")
        return 2

    log.info("拉取到 %d 条合约", len(contracts))
    log.info("样例记录: %s", contracts[0])

    # 基础统计
    by_underlying: dict[str, int] = {}
    by_callput: dict[str, int] = {}
    for c in contracts:
        by_underlying[c["underlying"]] = by_underlying.get(c["underlying"], 0) + 1
        by_callput[c["call_put"]] = by_callput.get(c["call_put"], 0) + 1
    log.info("按标的统计: %s", by_underlying)
    log.info("按类型统计: %s", by_callput)

    log.info("Step 2: 写入 PostgreSQL options_contracts 表")
    writer = DbWriter(
        host="localhost",
        port=5432,
        dbname=os.environ.get("DB_NAME", "volsurf"),
        user=os.environ.get("DB_USER", "volsurf"),
        password=os.environ.get("DB_PASSWORD", ""),
    )
    written = writer.upsert_option_contracts(contracts)
    log.info("成功写入 %d 条", written)

    log.info("Step 3: 验证数据库内容")
    import psycopg2

    with psycopg2.connect(
        host="localhost", port=5432, dbname=os.environ.get("DB_NAME", "volsurf"),
        user=os.environ.get("DB_USER", "volsurf"),
        password=os.environ.get("DB_PASSWORD", ""),
    ) as conn:
        with conn.cursor() as cur:
            cur.execute('SELECT COUNT(*) FROM options_contracts')
            total = cur.fetchone()[0]
            cur.execute("""
                SELECT "Underlying", COUNT(*), MIN("MaturityDate"),
                       MAX("MaturityDate"), MIN("ExercisePrice"),
                       MAX("ExercisePrice")
                FROM options_contracts
                GROUP BY "Underlying"
                ORDER BY "Underlying"
            """)
            rows = cur.fetchall()
            log.info("数据库当前总条数: %d", total)
            for r in rows:
                log.info("  Underlying=%s count=%s maturity=[%s..%s] strike=[%s..%s]",
                         r[0], r[1], r[2], r[3], r[4], r[5])

    log.info("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
