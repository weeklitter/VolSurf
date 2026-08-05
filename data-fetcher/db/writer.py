"""PostgreSQL 数据写入器：所有写入操作走 upsert，幂等。"""
from __future__ import annotations

import logging
from contextlib import contextmanager
from datetime import date, datetime
from typing import Iterable

import psycopg2
from psycopg2.extras import execute_values

logger = logging.getLogger(__name__)


class DbWriter:
    def __init__(self, host: str, port: int, dbname: str, user: str, password: str):
        self.conn_params = {
            "host": host,
            "port": port,
            "dbname": dbname,
            "user": user,
            "password": password,
        }

    # ── 连接管理 ─────────────────────────────────────────────────────────
    @contextmanager
    def _conn(self):
        conn = psycopg2.connect(**self.conn_params)
        try:
            yield conn
            conn.commit()
        except Exception:
            conn.rollback()
            raise
        finally:
            conn.close()

    # ── 期权日线 ─────────────────────────────────────────────────────────
    def write_option_daily(self, records: list[dict]) -> int:
        if not records:
            return 0

        rows = [
            (
                r.get("ts_code"),
                r.get("trade_date"),
                r.get("underlying", ""),
                r.get("open"),
                r.get("high"),
                r.get("low"),
                r.get("close"),
                r.get("settle"),
                r.get("vol"),
                r.get("amount"),
                r.get("oi"),
            )
            for r in records
        ]

        sql = """
        INSERT INTO options_daily
            (ts_code, trade_date, underlying, open, high, low, close, settle, vol, amount, oi)
        VALUES %s
        ON CONFLICT (ts_code, trade_date) DO UPDATE SET
            underlying = EXCLUDED.underlying,
            open       = EXCLUDED.open,
            high       = EXCLUDED.high,
            low        = EXCLUDED.low,
            close      = EXCLUDED.close,
            settle     = EXCLUDED.settle,
            vol        = EXCLUDED.vol,
            amount     = EXCLUDED.amount,
            oi         = EXCLUDED.oi;
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        logger.info("Upserted %d option_daily records", len(rows))
        return len(rows)

    # ── 期权合约信息 ─────────────────────────────────────────────────────
    def upsert_option_contracts(self, records: Iterable[dict]) -> int:
        rows = [
            (
                r.get("ts_code"),
                r.get("symbol"),
                r.get("exchange"),
                r.get("name"),
                r.get("underlying"),
                r.get("call_put"),
                r.get("exercise_price"),
                r.get("exercise_type", "欧式"),
                r.get("opt_multiplier", 1),
                r.get("maturity_date"),
                r.get("list_date"),
                r.get("delist_date"),
                bool(r.get("adjusted", False)),
            )
            for r in records
        ]
        if not rows:
            return 0

        sql = """
        INSERT INTO options_contracts
            (ts_code, symbol, exchange, name, underlying, call_put,
             exercise_price, exercise_type, opt_multiplier,
             maturity_date, list_date, delist_date, adjusted,
             created_at, updated_at)
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW(), NOW())
        ON CONFLICT (ts_code) DO UPDATE SET
            symbol         = EXCLUDED.symbol,
            exchange       = EXCLUDED.exchange,
            name           = EXCLUDED.name,
            underlying     = EXCLUDED.underlying,
            call_put       = EXCLUDED.call_put,
            exercise_price = EXCLUDED.exercise_price,
            exercise_type  = EXCLUDED.exercise_type,
            opt_multiplier = EXCLUDED.opt_multiplier,
            maturity_date  = EXCLUDED.maturity_date,
            adjusted       = EXCLUDED.adjusted,
            updated_at     = NOW();
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        logger.info("Upserted %d contract records", len(rows))
        return len(rows)

    # ── 标的日线 ─────────────────────────────────────────────────────────
    def write_underlying_daily(self, ts_code: str, trade_date: str, close: float) -> int:
        sql = """
        INSERT INTO underlying_daily (ts_code, trade_date, close)
        VALUES (%s, %s, %s)
        ON CONFLICT (ts_code, trade_date) DO UPDATE SET
            close = EXCLUDED.close;
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                cur.execute(sql, (ts_code, trade_date, close))
        logger.info("Upserted underlying_daily %s %s close=%s", ts_code, trade_date, close)
        return 1

    # ── 期权合约信息（PascalCase 列名对齐 EF Core 生成的 options_contracts） ─
    def write_option_contracts(self, records: Iterable[dict]) -> int:
        """批量 upsert 期权合约信息到 options_contracts。

        列名：TsCode / Symbol / Exchange / Name / Underlying / CallPut /
              ExercisePrice / ExerciseType / OptMultiplier /
              MaturityDate / ListDate / DelistDate / Adjusted
              / CreatedAt / UpdatedAt
        """
        rows = []
        for r in records:
            # accept both camelCase (from new fetcher) and snake_case inputs
            ts_code = r.get("ts_code")
            if not ts_code:
                continue

            maturity_date = r.get("maturity_date")
            if maturity_date is None:
                # 无到期日无法建索引，跳过
                continue

            # maturity_date 可能是 date / datetime / str
            if isinstance(maturity_date, datetime):
                mdate = maturity_date.date()
            elif isinstance(maturity_date, date):
                mdate = maturity_date
            else:
                s = str(maturity_date)
                try:
                    mdate = datetime.strptime(s, "%Y-%m-%d").date()
                except ValueError:
                    try:
                        mdate = datetime.strptime(s, "%Y%m%d").date()
                    except ValueError:
                        continue

            list_date = r.get("list_date")
            ldate = None
            if list_date is not None:
                if isinstance(list_date, datetime):
                    ldate = list_date.date()
                elif isinstance(list_date, date):
                    ldate = list_date
                else:
                    try:
                        ldate = datetime.strptime(str(list_date), "%Y-%m-%d").date()
                    except ValueError:
                        ldate = None

            delist_date = r.get("delist_date")
            ddate = None
            if delist_date is not None:
                if isinstance(delist_date, datetime):
                    ddate = delist_date.date()
                elif isinstance(delist_date, date):
                    ddate = delist_date
                else:
                    try:
                        ddate = datetime.strptime(str(delist_date), "%Y-%m-%d").date()
                    except ValueError:
                        ddate = None

            rows.append(
                (
                    ts_code,
                    r.get("symbol") or "",
                    r.get("exchange") or "SSE",
                    r.get("name") or "",
                    r.get("underlying") or "",
                    r.get("call_put") or "C",
                    r.get("exercise_price"),
                    r.get("exercise_type") or "欧式",
                    r.get("opt_multiplier") if r.get("opt_multiplier") is not None else 1,
                    mdate,
                    ldate,
                    ddate,
                    bool(r.get("adjusted", False)),
                )
            )

        if not rows:
            return 0

        # execute_values 仅支持单 VALUES 占位符 %s，这里改用 executemany
        sql = """
        INSERT INTO options_contracts
            ("TsCode", "Symbol", "Exchange", "Name", "Underlying", "CallPut",
             "ExercisePrice", "ExerciseType", "OptMultiplier",
             "MaturityDate", "ListDate", "DelistDate", "Adjusted",
             "CreatedAt", "UpdatedAt")
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW(), NOW())
        ON CONFLICT ("TsCode") DO UPDATE SET
            "Symbol"         = EXCLUDED."Symbol",
            "Exchange"       = EXCLUDED."Exchange",
            "Name"           = EXCLUDED."Name",
            "Underlying"     = EXCLUDED."Underlying",
            "CallPut"        = EXCLUDED."CallPut",
            "ExercisePrice"  = EXCLUDED."ExercisePrice",
            "ExerciseType"   = EXCLUDED."ExerciseType",
            "OptMultiplier"  = EXCLUDED."OptMultiplier",
            "MaturityDate"   = EXCLUDED."MaturityDate",
            "ListDate"       = EXCLUDED."ListDate",
            "Adjusted"       = EXCLUDED."Adjusted",
            "UpdatedAt"      = NOW();
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                cur.executemany(sql, rows)
        logger.info("Upserted %d option_contracts records", len(rows))
        return len(rows)

    # ── 标的基础信息 ─────────────────────────────────────────────────────
    def upsert_underlying(self, ts_code: str, name: str, exchange: str, asset_class: str, sort_order: int = 0) -> int:
        sql = """
        INSERT INTO underlyings (ts_code, name, exchange, asset_class, sort_order)
        VALUES (%s, %s, %s, %s, %s)
        ON CONFLICT (ts_code) DO UPDATE SET
            name       = EXCLUDED.name,
            exchange   = EXCLUDED.exchange,
            asset_class= EXCLUDED.asset_class,
            sort_order = EXCLUDED.sort_order;
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                cur.execute(sql, (ts_code, name, exchange, asset_class, sort_order))
        return 1