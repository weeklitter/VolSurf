"""AKShare 数据拉取器。

接口列表：
  - fetch_option_contracts(): 上交所当日期权合约清单（ak.option_current_day_sse）
  - fetch_underlying_daily(): ETF 价格（ak.fund_etf_hist_sina）
  - fetch_option_daily / fetch_option_basic / check_data_freshness:
    MVP 阶段由 TushareFetcher 负责，AKShare 仅作备份占位
"""
from __future__ import annotations

import logging
import re
from datetime import date, datetime
from typing import Optional

import numpy as np
import pandas as pd

from .base_fetcher import BaseFetcher

logger = logging.getLogger(__name__)


# 行权价/合约单位等数字字段可能在AKShare返回中带千位分隔符，移除后转 float
def _parse_decimal(value) -> Optional[float]:
    """兼容 str / float / NaN / None -> float 或 None。"""
    if value is None:
        return None
    if isinstance(value, float) and np.isnan(value):
        return None
    if isinstance(value, (np.floating,)):
        return float(value) if not np.isnan(value) else None
    if isinstance(value, (np.integer,)):
        return float(value)
    s = str(value).strip().replace(",", "")
    if not s or s.lower() in ("nan", "none", "null"):
        return None
    try:
        return float(s)
    except ValueError:
        return None


def _parse_date(value) -> Optional[date]:
    """YYYYMMDD / YYYY-MM-DD / datetime / NaN -> date 或 None。"""
    if value is None:
        return None
    if isinstance(value, float) and np.isnan(value):
        return None
    if isinstance(value, (np.floating,)):
        return None if np.isnan(value) else None
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
    logger.warning("无法解析日期字段: %r", s)
    return None


def _extract_underlying_code(text: str) -> str:
    """从 '50ETF(510050)' 这类文本中提取括号里的标的代码。"""
    if not text:
        return ""
    m = re.search(r"\(([\w]+)\)", text)
    if m:
        return m.group(1)
    return text.strip()


class AkshareFetcher(BaseFetcher):
    """AKShare 数据源。"""

    def __init__(self) -> None:
        try:
            import akshare as ak  # noqa: F401

            self._enabled = True
        except ImportError:
            logger.warning("akshare 未安装，AkshareFetcher 不可用")
            self._enabled = False

    # ── 期权合约信息（上交所） ───────────────────────────────────────────
    def fetch_option_contracts(self, exchange: str = "SSE") -> list[dict]:
        """拉取上交所当日全部活跃期权合约。

        Args:
            exchange: 当前实现仅支持 "SSE"（上交所）；
        Returns:
            list[dict],字段对齐 options_contracts 表（camelCase）：
              ts_code / symbol / exchange / name / underlying / call_put
              exercise_price / exercise_type / opt_multiplier
              maturity_date / list_date / delist_date / adjusted
        """
        if not self._enabled:
            logger.error("AkshareFetcher 未启用，无法拉取合约信息")
            return []

        if exchange != "SSE":
            logger.warning("当前实现仅支持上交所 SSE，requested=%s", exchange)
            return []

        try:
            import akshare as ak
        except ImportError:
            logger.error("akshare 不可用")
            return []

        try:
            df = ak.option_current_day_sse()
        except Exception as exc:  # noqa: BLE001
            logger.error("ak.option_current_day_sse 调用失败: %s", exc)
            return []

        if df is None or len(df) == 0:
            logger.warning("ak.option_current_day_sse 返回空 DataFrame")
            return []

        # NaN -> None（整列一次性替换，避免逐元素分支）
        df = df.replace({np.nan: None})

        records: list[dict] = []
        for _, row in df.iterrows():
            ts_code_raw = row.get("合约编码")
            symbol = row.get("合约交易代码")
            if not ts_code_raw or not symbol:
                continue

            # 合约编码 8 位数字 -> ts_code 加 .SH 后缀，与 Tushare 风格一致
            ts_code = f"{str(ts_code_raw).strip()}.SH"

            call_put_raw = str(row.get("类型") or "").strip()
            if call_put_raw in ("认购", "Call", "C"):
                call_put = "C"
            elif call_put_raw in ("认沽", "Put", "P"):
                call_put = "P"
            else:
                logger.warning("未知类型字段: %r, 跳过 %s", call_put_raw, ts_code)
                continue

            underlying_field = row.get("标的券名称及代码") or ""
            underlying = _extract_underlying_code(str(underlying_field))

            maturity_date = _parse_date(row.get("期权行权日") or row.get("到期日"))
            if maturity_date is None:
                logger.warning("合约 %s 缺少到期日，跳过", ts_code)
                continue

            list_date = _parse_date(row.get("开始日期"))
            # 上交所无对应 delist_date 字段，期权一般到期日即最后交易日，略大于到期日
            delist_date = maturity_date

            record = {
                "ts_code": ts_code,
                "symbol": str(symbol).strip(),
                "exchange": "SSE",
                "name": str(row.get("合约简称") or "").strip(),
                "underlying": underlying,
                "call_put": call_put,
                "exercise_price": _parse_decimal(row.get("行权价")),
                "exercise_type": "欧式",
                "opt_multiplier": _parse_decimal(row.get("合约单位")),
                "maturity_date": maturity_date,
                "list_date": list_date,
                "delist_date": delist_date,
                "adjusted": False,
            }
            records.append(record)

        logger.info("ak.option_current_day_sse 拉取完成: %d 条", len(records))
        return records

    # ── 基类抽象方法（MVP 阶段占位，由 TushareFetcher 负责） ──────────────
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
