"""PostgreSQL 数据写入器：所有写入操作走 upsert，幂等。"""
from __future__ import annotations

import logging
from contextlib import contextmanager
from datetime import date, datetime
from decimal import Decimal
from typing import Iterable, Optional

import psycopg2
from psycopg2.extras import execute_values

logger = logging.getLogger(__name__)


# ═══════════════════════════════════════════════════════════════════════════
# 类型转换 helpers
# ═══════════════════════════════════════════════════════════════════════════
def _to_date(value) -> Optional[date]:
    """YYYYMMDD / YYYY-MM-DD / date / datetime / NaN -> date 或 None。"""
    if value is None:
        return None
    if isinstance(value, float):  # NaN
        return None
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, date):
        return value
    s = str(value).strip()
    if not s or s.lower() in ("nan", "none", "null"):
        return None
    for fmt in ("%Y%m%d", "%Y-%m-%d", "%Y/%m/%d"):
        try:
            return datetime.strptime(s, fmt).date()
        except ValueError:
            continue
    return None


def _to_str(value, default: str = "") -> str:
    """NaN / None -> default;否则转 str。"""
    if value is None:
        return default
    if isinstance(value, float):
        return default
    s = str(value).strip()
    if not s or s.lower() in ("nan", "none", "null"):
        return default
    return s


def _to_decimal(value) -> Optional[Decimal]:
    """None / NaN / np.* / str / Decimal -> Decimal 或 None。"""
    if value is None:
        return None
    try:
        import numpy as np  # noqa: WPS433
    except ImportError:
        np = None  # type: ignore[assignment]

    if np is not None:
        if isinstance(value, np.floating) and np.isnan(value):
            return None
        if isinstance(value, np.integer):
            return Decimal(int(value))
        if isinstance(value, np.floating):
            return Decimal(str(float(value)))

    if isinstance(value, Decimal):
        return value
    if isinstance(value, float):
        if value != value:  # NaN
            return None
        return Decimal(str(value))
    if isinstance(value, int):
        return Decimal(value)

    s = str(value).strip().replace(",", "")
    if not s or s.lower() in ("nan", "none", "null"):
        return None
    try:
        return Decimal(s)
    except Exception:  # noqa: BLE001
        return None


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

    # ── 期权日线（PascalCase 列名对齐 EF Core 生成的 options_daily） ──
    def write_option_daily(self, records: list[dict]) -> int:
        """批量 upsert 期权日线行情到 options_daily。

        表 schema（来自 EF Core Migration 20260805065740_InitialCreate）：
            "TsCode"        varchar(30) PK
            "TradeDate"     date        PK
            "Underlying"    varchar(20)
            "Open"/"High"/"Low"/"Close"/"Settle"  numeric(10,4)
            "Vol"/"Amount"/"Oi"                   numeric(15,*)
        记录字段名延续 camelCase（fetcher 输出），写入前转 date 类型。
        """
        rows: list[tuple] = []
        for r in records:
            ts_code = r.get("ts_code")
            if not ts_code:
                continue

            trade_date = _to_date(r.get("trade_date"))
            if trade_date is None:
                # Tushare 返回的 trade_date 是 "20260804"，到这里应该是 date/str/datetime
                logger.warning("write_option_daily: %s 缺少 trade_date, 跳过", ts_code)
                continue

            rows.append(
                (
                    ts_code,
                    trade_date,
                    _to_str(r.get("underlying"), ""),
                    _to_decimal(r.get("open")),
                    _to_decimal(r.get("high")),
                    _to_decimal(r.get("low")),
                    _to_decimal(r.get("close")),
                    _to_decimal(r.get("settle")),
                    _to_decimal(r.get("vol")),
                    _to_decimal(r.get("amount")),
                    _to_decimal(r.get("oi")),
                )
            )
        if not rows:
            return 0

        sql = """
        INSERT INTO options_daily
            ("TsCode", "TradeDate", "Underlying",
             "Open", "High", "Low", "Close", "Settle",
             "Vol", "Amount", "Oi")
        VALUES %s
        ON CONFLICT ("TsCode", "TradeDate") DO UPDATE SET
            "Underlying" = EXCLUDED."Underlying",
            "Open"       = EXCLUDED."Open",
            "High"       = EXCLUDED."High",
            "Low"        = EXCLUDED."Low",
            "Close"      = EXCLUDED."Close",
            "Settle"     = EXCLUDED."Settle",
            "Vol"        = EXCLUDED."Vol",
            "Amount"     = EXCLUDED."Amount",
            "Oi"         = EXCLUDED."Oi";
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        logger.info("Upserted %d options_daily records", len(rows))
        return len(rows)

    # ── 标的日线（PascalCase 列名对齐 EF Core 生成的 underlying_daily） ──
    def write_underlying_daily(
        self, ts_code: str, trade_date, close: float
    ) -> int:
        """upsert 标的收盘价到 underlying_daily。

        支持 trade_date 传入 str / date / datetime；只接受 6 位以内的 close。
        """
        td = _to_date(trade_date)
        if td is None:
            logger.warning("write_underlying_daily: %s 无效 trade_date=%r", ts_code, trade_date)
            return 0
        cl = _to_decimal(close)
        if cl is None:
            logger.warning("write_underlying_daily: %s 无效 close=%r", ts_code, close)
            return 0
        sql = """
        INSERT INTO underlying_daily ("TsCode", "TradeDate", "Close")
        VALUES (%s, %s, %s)
        ON CONFLICT ("TsCode", "TradeDate") DO UPDATE SET
            "Close" = EXCLUDED."Close";
        """
        with self._conn() as conn:
            with conn.cursor() as cur:
                cur.execute(sql, (ts_code, td, cl))
        logger.info(
            "Upserted underlying_daily %s %s close=%s", ts_code, td, cl
        )
        return 1

    # ── 期权合约信息（PascalCase 列名对齐 EF Core 生成的 options_contracts） ─
    def upsert_option_contracts(self, records: Iterable[dict]) -> int:
        """批量 upsert 期权合约信息到 options_contracts。

        列名：TsCode / Symbol / Exchange / Name / Underlying / CallPut /
              ExercisePrice / ExerciseType / OptMultiplier /
              MaturityDate / ListDate / DelistDate / Adjusted
              / CreatedAt / UpdatedAt
        记录字段名延续 camelCase（fetcher 输出），写入前转 date / Decimal。
        """
        rows: list[tuple] = []
        for r in records:
            ts_code = r.get("ts_code")
            if not ts_code:
                continue

            maturity_date = _to_date(r.get("maturity_date"))
            if maturity_date is None:
                # 无到期日无法建索引，跳过
                continue

            list_date = _to_date(r.get("list_date"))
            delist_date = _to_date(r.get("delist_date"))

            opt_multiplier = _to_decimal(r.get("opt_multiplier")) or Decimal(1)

            rows.append(
                (
                    ts_code,
                    _to_str(r.get("symbol"), ""),
                    _to_str(r.get("exchange"), "SSE"),
                    _to_str(r.get("name"), ""),
                    _to_str(r.get("underlying"), ""),
                    _to_str(r.get("call_put"), "C")[:1],  # CallPut 是 char(1)
                    _to_decimal(r.get("exercise_price")),
                    _to_str(r.get("exercise_type"), "欧式"),
                    opt_multiplier,
                    maturity_date,
                    list_date,
                    delist_date,
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