"""Tushare 数据拉取器。

主要拉取接口：
  - opt_daily   期权日线
  - opt_basic   期权合约基本信息
  - daily       指数日线（用于沪深 300 股指标的）
"""
from __future__ import annotations

import logging
from typing import Optional

import numpy as np
import pandas as pd
import tushare as ts

from .base_fetcher import BaseFetcher

logger = logging.getLogger(__name__)


# ═══════════════════════════════════════════════════════════════════════════
# 清洗工具
# ═══════════════════════════════════════════════════════════════════════════
def clean_dataframe(df: pd.DataFrame) -> list[dict]:
    """清洗 Tushare DataFrame：NaN→None、日期转换、numpy→原生类型。"""
    if df is None or len(df) == 0:
        return []

    # 1. NaN → None（否则 psycopg2 会写入字符串 "nan"）
    df = df.replace({np.nan: None})

    # 2. 日期字段：Tushare 字符串 "20260731" → date 对象
    for date_col in ("trade_date", "maturity_date", "list_date", "delist_date"):
        if date_col in df.columns:
            df[date_col] = pd.to_datetime(
                df[date_col], format="%Y%m%d", errors="coerce"
            ).dt.date

    # 3. numpy 类型 → Python 原生
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
        cleaned.append(clean_r)
    return cleaned


# ═══════════════════════════════════════════════════════════════════════════
# TushareFetcher
# ═══════════════════════════════════════════════════════════════════════════
class TushareFetcher(BaseFetcher):
    """Tushare 数据源（主力）。"""

    def __init__(self, token: str):
        if not token:
            raise ValueError("TUSHARE_TOKEN 未配置")
        ts.set_token(token)
        self.pro = ts.pro_api(token)

    def fetch_option_daily(self, trade_date: str) -> list[dict]:
        results: list[dict] = []
        for exchange in ("SSE", "SZSE", "CFFEX"):
            try:
                df = self.pro.opt_daily(trade_date=trade_date, exchange=exchange)
                if df is not None and len(df) > 0:
                    # 添加 underlying 冗余字段（从合约表里来，但 Tushare 数据已经带）
                    results.extend(clean_dataframe(df))
            except Exception as exc:  # noqa: BLE001
                logger.error("opt_daily 拉取失败 exchange=%s err=%s", exchange, exc)
        return results

    def fetch_option_basic(self, exchange: Optional[str] = None) -> list[dict]:
        params: dict = {}
        if exchange:
            params["exchange"] = exchange
        try:
            df = self.pro.opt_basic(**params)
            return clean_dataframe(df)
        except Exception as exc:  # noqa: BLE001
            logger.error("opt_basic 拉取失败 exchange=%s err=%s", exchange, exc)
            return []

    def fetch_underlying_daily(self, ts_code: str, trade_date: str) -> Optional[dict]:
        """标的日线：ETF 用 AKShare，股指用 Tushare daily。

        ETF: 510050 / 510300 → AKShare fund_etf_hist_sina
        股指: 000300 → Tushare daily(ts_code='000300.SH')
        """
        if ts_code.startswith("5"):
            return self._fetch_etf_price_akshare(ts_code, trade_date)
        return self._fetch_index_price_tushare(ts_code, trade_date)

    def _fetch_etf_price_akshare(self, ts_code: str, trade_date: str) -> Optional[dict]:
        try:
            import akshare as ak  # 延迟导入，避免部分环境未安装时影响 Tushare 路径

            # 510050 → sh510050
            symbol = f"sh{ts_code}" if ts_code.startswith("5") else f"sz{ts_code}"
            df = ak.fund_etf_hist_sina(symbol=symbol)
            if df is None or len(df) == 0:
                logger.warning("AKShare: 无 ETF 数据 %s", ts_code)
                return None

            date_str = pd.to_datetime(trade_date, format="%Y%m%d").strftime("%Y-%m-%d")
            row = df[df["date"] == date_str]
            if len(row) == 0:
                logger.warning("AKShare: %s 在 %s 无数据", ts_code, date_str)
                return None
            return {
                "ts_code": ts_code,
                "trade_date": date_str.replace("-", ""),
                "close": float(row.iloc[0]["close"]),
            }
        except Exception as exc:  # noqa: BLE001
            logger.error("AKShare 拉取 ETF 失败 %s: %s", ts_code, exc)
            return None

    def _fetch_index_price_tushare(self, ts_code: str, trade_date: str) -> Optional[dict]:
        try:
            tushare_code = f"{ts_code}.SH" if ts_code.startswith("0") else ts_code
            df = self.pro.daily(ts_code=tushare_code, trade_date=trade_date)
            if df is None or len(df) == 0:
                logger.warning("Tushare: 无指数数据 %s %s", ts_code, trade_date)
                return None
            return {
                "ts_code": ts_code,
                "trade_date": trade_date,
                "close": float(df.iloc[0]["close"]),
            }
        except Exception as exc:  # noqa: BLE001
            logger.error("Tushare 拉取指数失败 %s: %s", ts_code, exc)
            return None

    def check_data_freshness(self, trade_date: str) -> bool:
        try:
            data = self.fetch_option_daily(trade_date)
            return len(data) > 0
        except Exception as exc:  # noqa: BLE001
            logger.error("freshness check failed: %s", exc)
            return False