"""AKShare 数据拉取器（备份源）。

MVP 阶段暂未启用——Tushare 为主力数据源，AKShare 仅作为 ETF 价格备份。
所有方法占位实现，避免上层 import 失败。
"""
from __future__ import annotations

import logging
from typing import Optional

from .base_fetcher import BaseFetcher

logger = logging.getLogger(__name__)


class AkshareFetcher(BaseFetcher):
    """AKShare 数据源（备份）。"""

    def __init__(self) -> None:
        try:
            import akshare as ak  # noqa: F401

            self._enabled = True
        except ImportError:
            logger.warning("akshare 未安装，AkshareFetcher 不可用")
            self._enabled = False

    def fetch_option_daily(self, trade_date: str) -> list[dict]:
        logger.warning("AkshareFetcher.fetch_option_daily 暂未实现，请使用 TushareFetcher")
        return []

    def fetch_option_basic(self, exchange: Optional[str] = None) -> list[dict]:
        logger.warning("AkshareFetcher.fetch_option_basic 暂未实现，请使用 TushareFetcher")
        return []

    def fetch_underlying_daily(self, ts_code: str, trade_date: str) -> Optional[dict]:
        """ETF 价格：fund_etf_hist_sina。"""
        if not self._enabled:
            return None
        try:
            import akshare as ak
            import pandas as pd

            symbol = f"sh{ts_code}" if ts_code.startswith("5") else f"sz{ts_code}"
            df = ak.fund_etf_hist_sina(symbol=symbol)
            if df is None or len(df) == 0:
                return None
            date_str = pd.to_datetime(trade_date, format="%Y%m%d").strftime("%Y-%m-%d")
            row = df[df["date"] == date_str]
            if len(row) == 0:
                return None
            return {
                "ts_code": ts_code,
                "trade_date": trade_date,
                "close": float(row.iloc[0]["close"]),
            }
        except Exception as exc:  # noqa: BLE001
            logger.error("AKShare ETF 拉取失败 %s: %s", ts_code, exc)
            return None

    def check_data_freshness(self, trade_date: str) -> bool:
        return False  # MVP 阶段 AKShare 不做新鲜度校验