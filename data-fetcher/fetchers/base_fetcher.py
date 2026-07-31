"""数据拉取抽象基类。

TushareFetcher / AkshareFetcher 共享相同的清洗逻辑与数据契约。
"""
from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Optional


class BaseFetcher(ABC):
    """数据源抽象基类。"""

    @abstractmethod
    def fetch_option_daily(self, trade_date: str) -> list[dict]:
        """拉取期权日线行情。

        Args:
            trade_date: 交易日期 YYYYMMDD
        Returns:
            清洗后的记录列表
        """

    @abstractmethod
    def fetch_option_basic(self, exchange: Optional[str] = None) -> list[dict]:
        """拉取期权合约信息。"""

    @abstractmethod
    def fetch_underlying_daily(self, ts_code: str, trade_date: str) -> Optional[dict]:
        """拉取标的价格（ETF / 股指）。"""

    @abstractmethod
    def check_data_freshness(self, trade_date: str) -> bool:
        """检查当日数据是否已发布。"""