"""fetch_stocks.py - VolSurf 股票数据采集脚本。

拉取沪深300成分股的基础信息、日线行情、每日指标(PE/PB等)、
三大报表(利润表/资产负债表/现金流量表)、主营业务构成。

数据源：Tushare Pro API（需2000积分）
频率控制：每次调用后 sleep(0.15)（500次/分钟限制）
首次全量回填预估：20-40分钟

使用方式：
  python3 fetch_stocks.py                          # 增量更新（拉最近数据）
  python3 fetch_stocks.py --backfill               # 全量回填（拉3-5年历史）
  python3 fetch_stocks.py --ts-code 600519.SH      # 只拉单只股票
  python3 fetch_stocks.py --limit 5                # 只拉前5只（测试用）

参照现有 data-fetcher/ 下的代码风格：
  - 使用 DbWriter 已有的 _to_date() / _to_decimal() / _to_str() 类型转换
  - upsert SQL 使用 PascalCase 列名（对齐 EF Core Migration 生成的列名）
  - 限流 sleep(0.15)
  - try-catch 错误处理，单个股票失败不影响整体
  - 进度日志：每10只股票打印一次进度

Tushare 接口对应关系：
  stock_basic   -> stock_basic 表
  daily         -> stock_daily 表
  daily_basic   -> stock_daily_basic 表
  income        -> stock_income 表
  balancesheet  -> stock_balance_sheet 表
  cashflow      -> stock_cashflow 表（CapEx 字段 = c_pay_acquisition_const_ppe）
"""
from __future__ import annotations

import argparse
import logging
import os
import sys
import time
from datetime import datetime, timedelta
from decimal import Decimal
from typing import Optional

import numpy as np
import pandas as pd
import tushare as ts

# 复用现有 data-fetcher 的基础设施
from config import Config
from db.writer import DbWriter, _to_date, _to_decimal, _to_str

logger = logging.getLogger(__name__)

# ═══════════════════════════════════════════════════════════════════════════
# 配置
# ═══════════════════════════════════════════════════════════════════════════
RATE_LIMIT_SLEEP = 0.15  # 秒，500次/分钟限制 -> 每次间隔至少0.12秒，留余量


# ═══════════════════════════════════════════════════════════════════════════
# StockFetcher: Tushare 数据拉取器
# ═══════════════════════════════════════════════════════════════════════════
class StockFetcher:
    """股票数据拉取器，封装 Tushare 股票相关接口。"""

    def __init__(self, token: str):
        if not token:
            raise ValueError("TUSHARE_TOKEN 未配置")
        ts.set_token(token)
        self.pro = ts.pro_api(token)

    # ── 沪深300成分股列表 ──────────────────────────────────────────────────
    def fetch_hs300_constituents(self) -> list[dict]:
        """获取沪深300成分股列表（约300只）。

        Tushare接口: index_weight
        参数: index_code='000300.SH', trade_date=最近交易日
        """
        try:
            # 获取最近交易日的沪深300成分权重
            for days_back in range(0, 30):
                check_date = (datetime.now() - timedelta(days=days_back)).strftime("%Y%m%d")
                df = self.pro.index_weight(index_code="000300.SH", trade_date=check_date)
                if df is not None and len(df) > 0:
                    df = df.replace({np.nan: None})
                    records = df.to_dict("records")
                    logger.info("沪深300成分股: %d 只 (日期=%s)", len(records), check_date)
                    return records
            logger.warning("未获取到沪深300成分股数据")
            return []
        except Exception as exc:
            logger.error("fetch_hs300_constituents 失败: %s", exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)

    # ── 股票基础信息 ───────────────────────────────────────────────────────
    def fetch_stock_basic(self, ts_code: Optional[str] = None) -> list[dict]:
        """获取股票基础信息。

        Tushare接口: stock_basic
        参数: 可选 ts_code 过滤单只股票
        """
        try:
            params = {"list_status": "L"}  # 只拉上市中的
            if ts_code:
                params["ts_code"] = ts_code
            df = self.pro.stock_basic(**params)
            if df is None or len(df) == 0:
                return []
            df = df.replace({np.nan: None})
            records = df.to_dict("records")
            logger.info("stock_basic: %d 条", len(records))
            return records
        except Exception as exc:
            logger.error("fetch_stock_basic 失败: %s", exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)

    # ── 日线行情 ───────────────────────────────────────────────────────────
    def fetch_daily(self, ts_code: str, start_date: str, end_date: str) -> list[dict]:
        """获取股票日线行情。

        Tushare接口: daily
        参数: ts_code, start_date, end_date (YYYYMMDD格式)
        """
        try:
            df = self.pro.daily(ts_code=ts_code, start_date=start_date, end_date=end_date)
            if df is None or len(df) == 0:
                return []
            df = df.replace({np.nan: None})
            return df.to_dict("records")
        except Exception as exc:
            logger.error("fetch_daily 失败 %s: %s", ts_code, exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)

    # ── 每日指标（PE/PB等）────────────────────────────────────────────────
    def fetch_daily_basic(self, ts_code: str, start_date: str, end_date: str) -> list[dict]:
        """获取每日指标（估值数据）。

        Tushare接口: daily_basic
        参数: ts_code, start_date, end_date
        返回字段: pe, pe_ttm, pb, ps, ps_ttm, total_mv, circ_mv, turnover_rate, dv_ratio
        """
        try:
            df = self.pro.daily_basic(
                ts_code=ts_code, start_date=start_date, end_date=end_date
            )
            if df is None or len(df) == 0:
                return []
            df = df.replace({np.nan: None})
            return df.to_dict("records")
        except Exception as exc:
            logger.error("fetch_daily_basic 失败 %s: %s", ts_code, exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)

    # ── 利润表 ─────────────────────────────────────────────────────────────
    def fetch_income(self, ts_code: str, start_date: str, end_date: str) -> list[dict]:
        """获取利润表。

        Tushare接口: income
        参数: ts_code, start_date, end_date (报告期范围)
        返回字段: total_revenue(营收), oper_cost(营业成本), gross_profit(毛利),
                 n_income(净利润), report_type, end_date, update_date
        """
        try:
            df = self.pro.income(
                ts_code=ts_code, start_date=start_date, end_date=end_date
            )
            if df is None or len(df) == 0:
                return []
            df = df.replace({np.nan: None})
            records = df.to_dict("records")
            return records
        except Exception as exc:
            logger.error("fetch_income 失败 %s: %s", ts_code, exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)

    # ── 资产负债表 ─────────────────────────────────────────────────────────
    def fetch_balancesheet(self, ts_code: str, start_date: str, end_date: str) -> list[dict]:
        """获取资产负债表。

        Tushare接口: balancesheet
        参数: ts_code, start_date, end_date
        返回字段: total_assets, total_liab, total_hldr_eqy_exc_min_int(股东权益),
                 goodwill, accounts_recv, inventories, report_type
        """
        try:
            df = self.pro.balancesheet(
                ts_code=ts_code, start_date=start_date, end_date=end_date
            )
            if df is None or len(df) == 0:
                return []
            df = df.replace({np.nan: None})
            return df.to_dict("records")
        except Exception as exc:
            logger.error("fetch_balancesheet 失败 %s: %s", ts_code, exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)

    # ── 现金流量表 ─────────────────────────────────────────────────────────
    def fetch_cashflow(self, ts_code: str, start_date: str, end_date: str) -> list[dict]:
        """获取现金流量表。

        Tushare接口: cashflow
        参数: ts_code, start_date, end_date
        返回字段: n_cashflow_act(经营现金流), n_cashflow_inv_act(投资现金流),
                 n_cash_flows_fnc_act(筹资现金流),
                 c_pay_acq_const_fiolta(资本支出,用于计算FCF)
        """
        try:
            df = self.pro.cashflow(
                ts_code=ts_code, start_date=start_date, end_date=end_date
            )
            if df is None or len(df) == 0:
                return []
            df = df.replace({np.nan: None})
            return df.to_dict("records")
        except Exception as exc:
            logger.error("fetch_cashflow 失败 %s: %s", ts_code, exc)
            return []
        finally:
            time.sleep(RATE_LIMIT_SLEEP)


# ═══════════════════════════════════════════════════════════════════════════
# StockDbWriter: 数据库写入器（股票表专用）
# ═══════════════════════════════════════════════════════════════════════════
class StockDbWriter(DbWriter):
    """扩展 DbWriter，新增股票表的 upsert 方法。

    列名使用 PascalCase（对齐 EF Core Migration 生成的列名，与现有期权表一致）。
    表名使用 snake_case（如 stock_basic, stock_daily）。
    """

    # ── 股票基础信息 ───────────────────────────────────────────────────────
    def upsert_stock_basic(self, records: list[dict]) -> int:
        """批量 upsert 股票基础信息到 stock_basic 表。"""
        rows = []
        for r in records:
            ts_code = _to_str(r.get("ts_code"))
            if not ts_code:
                continue

            # Tushare stock_basic 不返回 exchange 字段，从 ts_code 后缀推断
            exchange = _to_str(r.get("exchange"))
            if not exchange:
                if ts_code.endswith(".SH"):
                    exchange = "SSE"
                elif ts_code.endswith(".SZ"):
                    exchange = "SZSE"
                elif ts_code.endswith(".BJ"):
                    exchange = "BSE"

            rows.append((
                ts_code,
                _to_str(r.get("symbol")),
                _to_str(r.get("name")),
                _to_str(r.get("area")),
                _to_str(r.get("industry")),
                _to_str(r.get("market")),
                _to_date(r.get("list_date")),
                exchange,
            ))
        if not rows:
            return 0

        sql = """
        INSERT INTO stock_basic
            ("TsCode", "Symbol", "Name", "Area", "Industry", "Market", "ListDate", "Exchange")
        VALUES %s
        ON CONFLICT ("TsCode") DO UPDATE SET
            "Symbol"   = EXCLUDED."Symbol",
            "Name"     = EXCLUDED."Name",
            "Area"     = EXCLUDED."Area",
            "Industry" = EXCLUDED."Industry",
            "Market"   = EXCLUDED."Market",
            "ListDate" = EXCLUDED."ListDate",
            "Exchange" = EXCLUDED."Exchange";
        """
        from psycopg2.extras import execute_values
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        logger.info("Upserted %d stock_basic records", len(rows))
        return len(rows)

    # ── 日线行情 ───────────────────────────────────────────────────────────
    def upsert_stock_daily(self, records: list[dict]) -> int:
        """批量 upsert 股票日线行情到 stock_daily 表。"""
        # 去重（Tushare 可能返回同一 (ts_code, trade_date) 的多行）
        seen: dict[tuple, dict] = {}
        for r in records:
            ts_code = _to_str(r.get("ts_code"))
            trade_date = _to_date(r.get("trade_date"))
            if not ts_code or trade_date is None:
                continue
            pk = (ts_code, trade_date)
            seen[pk] = r

        rows = []
        for (ts_code, trade_date), r in seen.items():
            rows.append((
                ts_code, trade_date,
                _to_decimal(r.get("open")),
                _to_decimal(r.get("high")),
                _to_decimal(r.get("low")),
                _to_decimal(r.get("close")),
                _to_decimal(r.get("pre_close")),
                _to_decimal(r.get("change")),
                _to_decimal(r.get("pct_chg")),
                _to_decimal(r.get("vol")),
                _to_decimal(r.get("amount")),
            ))
        if not rows:
            return 0

        sql = """
        INSERT INTO stock_daily
            ("TsCode", "TradeDate", "Open", "High", "Low", "Close",
             "PreClose", "Change", "PctChg", "Vol", "Amount")
        VALUES %s
        ON CONFLICT ("TsCode", "TradeDate") DO UPDATE SET
            "Open"     = EXCLUDED."Open",
            "High"     = EXCLUDED."High",
            "Low"      = EXCLUDED."Low",
            "Close"    = EXCLUDED."Close",
            "PreClose" = EXCLUDED."PreClose",
            "Change"   = EXCLUDED."Change",
            "PctChg"   = EXCLUDED."PctChg",
            "Vol"      = EXCLUDED."Vol",
            "Amount"   = EXCLUDED."Amount";
        """
        from psycopg2.extras import execute_values
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        return len(rows)

    # ── 每日指标 ───────────────────────────────────────────────────────────
    def upsert_stock_daily_basic(self, records: list[dict]) -> int:
        """批量 upsert 每日指标到 stock_daily_basic 表。"""
        # 去重（同上）
        seen: dict[tuple, dict] = {}
        for r in records:
            ts_code = _to_str(r.get("ts_code"))
            trade_date = _to_date(r.get("trade_date"))
            if not ts_code or trade_date is None:
                continue
            pk = (ts_code, trade_date)
            seen[pk] = r

        rows = []
        for (ts_code, trade_date), r in seen.items():
            rows.append((
                ts_code, trade_date,
                _to_decimal(r.get("close")),
                _to_decimal(r.get("pe")),
                _to_decimal(r.get("pe_ttm")),
                _to_decimal(r.get("pb")),
                _to_decimal(r.get("ps")),
                _to_decimal(r.get("ps_ttm")),
                _to_decimal(r.get("total_mv")),
                _to_decimal(r.get("circ_mv")),
                _to_decimal(r.get("turnover_rate")),
                _to_decimal(r.get("dv_ratio")),
            ))
        if not rows:
            return 0

        sql = """
        INSERT INTO stock_daily_basic
            ("TsCode", "TradeDate", "Close", "Pe", "PeTtm", "Pb", "Ps", "PsTtm",
             "TotalMv", "CircMv", "TurnoverRate", "DvRatio")
        VALUES %s
        ON CONFLICT ("TsCode", "TradeDate") DO UPDATE SET
            "Close"        = EXCLUDED."Close",
            "Pe"           = EXCLUDED."Pe",
            "PeTtm"        = EXCLUDED."PeTtm",
            "Pb"           = EXCLUDED."Pb",
            "Ps"           = EXCLUDED."Ps",
            "PsTtm"        = EXCLUDED."PsTtm",
            "TotalMv"      = EXCLUDED."TotalMv",
            "CircMv"       = EXCLUDED."CircMv",
            "TurnoverRate" = EXCLUDED."TurnoverRate",
            "DvRatio"      = EXCLUDED."DvRatio";
        """
        from psycopg2.extras import execute_values
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        return len(rows)

    # ── 利润表 ─────────────────────────────────────────────────────────────
    def upsert_stock_income(self, records: list[dict]) -> int:
        """批量 upsert 利润表。

        Tushare字段映射:
          total_revenue -> Revenue
          oper_cost     -> OperCost
          gross_profit  -> GrossProfit（可能不存在，用 total_revenue - oper_cost 计算）
          n_income      -> NetProfit
          report_type   -> ReportType
          end_date      -> EndDate
          ann_date      -> UpdateDate
        """
        # Tushare 可能返回同一 (ts_code, end_date, report_type) 的多行，
        # 需要去重，否则 ON CONFLICT DO UPDATE 会报 CardinalityViolation。
        seen: dict[tuple, dict] = {}
        for r in records:
            ts_code = _to_str(r.get("ts_code"))
            end_date = _to_date(r.get("end_date"))
            report_type = _to_str(r.get("report_type"), "1")
            if not ts_code or end_date is None:
                continue
            pk = (ts_code, end_date, report_type)
            seen[pk] = r  # 后出现的覆盖前面的

        rows = []
        for (ts_code, end_date, report_type), r in seen.items():
            revenue = _to_decimal(r.get("total_revenue"))
            oper_cost = _to_decimal(r.get("oper_cost"))
            # 毛利润：优先用 Tushare 返回的 gross_profit，否则手动计算
            gross_profit = _to_decimal(r.get("grossprofit"))
            if gross_profit is None and revenue is not None and oper_cost is not None:
                gross_profit = revenue - oper_cost

            update_date = _to_date(r.get("ann_date")) or datetime.now().date()

            rows.append((
                ts_code, end_date, report_type,
                revenue, oper_cost, gross_profit,
                _to_decimal(r.get("n_income")),
                update_date,
            ))
        if not rows:
            return 0

        sql = """
        INSERT INTO stock_income
            ("TsCode", "EndDate", "ReportType", "Revenue", "OperCost",
             "GrossProfit", "NetProfit", "UpdateDate")
        VALUES %s
        ON CONFLICT ("TsCode", "EndDate", "ReportType") DO UPDATE SET
            "Revenue"     = EXCLUDED."Revenue",
            "OperCost"    = EXCLUDED."OperCost",
            "GrossProfit" = EXCLUDED."GrossProfit",
            "NetProfit"   = EXCLUDED."NetProfit",
            "UpdateDate"  = EXCLUDED."UpdateDate";
        """
        from psycopg2.extras import execute_values
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        return len(rows)

    # ── 资产负债表 ─────────────────────────────────────────────────────────
    def upsert_stock_balance_sheet(self, records: list[dict]) -> int:
        """批量 upsert 资产负债表。

        Tushare字段映射:
          total_assets                       -> TotalAssets
          total_liab                         -> TotalLiab
          total_hldr_eqy_exc_min_int         -> TotalEquity（归母股东权益）
          goodwill                           -> Goodwill
          accounts_recv                      -> AccountRecv（应收账款）
          inventories                        -> Inventory（存货）
        """
        # 去重（同上）
        seen: dict[tuple, dict] = {}
        for r in records:
            ts_code = _to_str(r.get("ts_code"))
            end_date = _to_date(r.get("end_date"))
            report_type = _to_str(r.get("report_type"), "1")
            if not ts_code or end_date is None:
                continue
            pk = (ts_code, end_date, report_type)
            seen[pk] = r

        rows = []
        for (ts_code, end_date, report_type), r in seen.items():
            update_date = _to_date(r.get("ann_date")) or datetime.now().date()

            rows.append((
                ts_code, end_date, report_type,
                _to_decimal(r.get("total_assets")),
                _to_decimal(r.get("total_liab")),
                _to_decimal(r.get("total_hldr_eqy_exc_min_int")),
                _to_decimal(r.get("goodwill")),
                _to_decimal(r.get("accounts_receiv")),
                _to_decimal(r.get("inventories")),
                update_date,
            ))
        if not rows:
            return 0

        sql = """
        INSERT INTO stock_balance_sheet
            ("TsCode", "EndDate", "ReportType", "TotalAssets", "TotalLiab",
             "TotalEquity", "Goodwill", "AccountRecv", "Inventory", "UpdateDate")
        VALUES %s
        ON CONFLICT ("TsCode", "EndDate", "ReportType") DO UPDATE SET
            "TotalAssets" = EXCLUDED."TotalAssets",
            "TotalLiab"   = EXCLUDED."TotalLiab",
            "TotalEquity" = EXCLUDED."TotalEquity",
            "Goodwill"    = EXCLUDED."Goodwill",
            "AccountRecv" = EXCLUDED."AccountRecv",
            "Inventory"   = EXCLUDED."Inventory",
            "UpdateDate"  = EXCLUDED."UpdateDate";
        """
        from psycopg2.extras import execute_values
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        return len(rows)

    # ── 现金流量表 ─────────────────────────────────────────────────────────
    def upsert_stock_cashflow(self, records: list[dict]) -> int:
        """批量 upsert 现金流量表。

        Tushare字段映射:
          n_cashflow_act             -> OperCashFlow（经营现金流）
          n_cashflow_inv_act         -> InvestCashFlow（投资现金流）
          n_cash_flows_fnc_act       -> FinCashFlow（筹资现金流）
          c_pay_acq_const_fiolta -> CapEx（资本支出，用于计算FCF）
          注：Tushare 现金流量表中购建固定资产等资本支出字段名为 c_pay_acq_const_fiolta
        """
        # 去重（同上）
        seen: dict[tuple, dict] = {}
        for r in records:
            ts_code = _to_str(r.get("ts_code"))
            end_date = _to_date(r.get("end_date"))
            report_type = _to_str(r.get("report_type"), "1")
            if not ts_code or end_date is None:
                continue
            pk = (ts_code, end_date, report_type)
            seen[pk] = r

        rows = []
        for (ts_code, end_date, report_type), r in seen.items():
            update_date = _to_date(r.get("ann_date")) or datetime.now().date()

            # CapEx: 优先 c_pay_acq_const_fiolta（当前 Tushare 实际字段名），
            # 兼容旧字段名 c_pay_acquisition_const_ppe
            capex = _to_decimal(r.get("c_pay_acq_const_fiolta"))
            if capex is None:
                capex = _to_decimal(r.get("c_pay_acquisition_const_ppe"))

            rows.append((
                ts_code, end_date, report_type,
                _to_decimal(r.get("n_cashflow_act")),
                _to_decimal(r.get("n_cashflow_inv_act")),
                _to_decimal(r.get("n_cash_flows_fnc_act")),
                capex,
                update_date,
            ))
        if not rows:
            return 0

        sql = """
        INSERT INTO stock_cashflow
            ("TsCode", "EndDate", "ReportType", "OperCashFlow", "InvestCashFlow",
             "FinCashFlow", "CapEx", "UpdateDate")
        VALUES %s
        ON CONFLICT ("TsCode", "EndDate", "ReportType") DO UPDATE SET
            "OperCashFlow"   = EXCLUDED."OperCashFlow",
            "InvestCashFlow" = EXCLUDED."InvestCashFlow",
            "FinCashFlow"    = EXCLUDED."FinCashFlow",
            "CapEx"          = EXCLUDED."CapEx",
            "UpdateDate"     = EXCLUDED."UpdateDate";
        """
        from psycopg2.extras import execute_values
        with self._conn() as conn:
            with conn.cursor() as cur:
                execute_values(cur, sql, rows, page_size=500)
        return len(rows)


# ═══════════════════════════════════════════════════════════════════════════
# 主流程
# ═══════════════════════════════════════════════════════════════════════════
def run_fetch(
    fetcher: StockFetcher,
    db_writer: StockDbWriter,
    backfill: bool = False,
    ts_code: Optional[str] = None,
    limit: Optional[int] = None,
):
    """执行数据采集主流程。

    Args:
        fetcher: Tushare 数据拉取器
        db_writer: 数据库写入器
        backfill: True=全量回填（拉3-5年历史），False=增量更新
        ts_code: 指定单只股票（调试用），None=拉全部沪深300
        limit: 限制拉取股票数量（测试用），None=不限制
    """
    start_time = time.time()

    # 1. 获取股票列表
    if ts_code:
        stocks = fetcher.fetch_stock_basic(ts_code)
    else:
        # 获取沪深300成分股
        logger.info("开始获取沪深300成分股列表...")
        hs300 = fetcher.fetch_hs300_constituents()
        if not hs300:
            logger.error("无法获取沪深300成分股，退出")
            return

        # 提取 ts_code 列表
        stock_codes = list(set(r.get("con_code") or r.get("ts_code") for r in hs300))
        stock_codes = [c for c in stock_codes if c]
        logger.info("共 %d 只股票需要采集", len(stock_codes))

        # 拉取基础信息
        all_basic = fetcher.fetch_stock_basic()
        stocks = [s for s in all_basic if s.get("ts_code") in stock_codes]

    if not stocks:
        logger.error("未获取到股票列表，退出")
        return

    # 应用 limit（测试用）
    if limit and limit > 0:
        stocks = stocks[:limit]
        logger.info("限制模式：只拉前 %d 只股票", limit)

    # 2. 写入基础信息
    db_writer.upsert_stock_basic(stocks)
    logger.info("Step 1: 股票基础信息写入完成 (%d 只)", len(stocks))

    # 3. 设置日期范围
    if backfill:
        daily_start = (datetime.now() - timedelta(days=365 * 3)).strftime("%Y%m%d")
        quarterly_start = (datetime.now() - timedelta(days=365 * 5)).strftime("%Y%m%d")
    else:
        daily_start = (datetime.now() - timedelta(days=7)).strftime("%Y%m%d")
        quarterly_start = (datetime.now() - timedelta(days=365)).strftime("%Y%m%d")
    end_date = datetime.now().strftime("%Y%m%d")

    # 4. 逐股票采集
    total = len(stocks)
    for idx, stock in enumerate(stocks, 1):
        code = stock.get("ts_code")
        if not code:
            continue

        name = stock.get("name", "")

        # 进度日志：每10只打印一次 + 第一只
        if idx == 1 or idx % 10 == 0 or idx == total:
            elapsed = time.time() - start_time
            rate = idx / elapsed if elapsed > 0 else 0
            eta = (total - idx) / rate if rate > 0 else 0
            logger.info(
                "进度 [%d/%d] (%.1f%%) 已耗时 %.0fs, 预计剩余 %.0fs | 当前: %s (%s)",
                idx, total, idx / total * 100, elapsed, eta, code, name,
            )

        try:
            # ── 日线行情 ──
            daily_data = fetcher.fetch_daily(code, daily_start, end_date)
            if daily_data:
                db_writer.upsert_stock_daily(daily_data)

            # ── 每日指标 ──
            basic_data = fetcher.fetch_daily_basic(code, daily_start, end_date)
            if basic_data:
                db_writer.upsert_stock_daily_basic(basic_data)

            # ── 利润表 ──
            income_data = fetcher.fetch_income(code, quarterly_start, end_date)
            if income_data:
                db_writer.upsert_stock_income(income_data)

            # ── 资产负债表 ──
            bs_data = fetcher.fetch_balancesheet(code, quarterly_start, end_date)
            if bs_data:
                db_writer.upsert_stock_balance_sheet(bs_data)

            # ── 现金流量表 ──
            cf_data = fetcher.fetch_cashflow(code, quarterly_start, end_date)
            if cf_data:
                db_writer.upsert_stock_cashflow(cf_data)

            if idx <= 5 or idx % 10 == 0:
                logger.debug(
                    "[%d/%d] %s 采集完成: daily=%d, basic=%d, income=%d, bs=%d, cf=%d",
                    idx, total, code,
                    len(daily_data), len(basic_data),
                    len(income_data), len(bs_data), len(cf_data),
                )

        except Exception as exc:
            logger.error("[%d/%d] %s 采集失败: %s", idx, total, code, exc, exc_info=True)
            # 单只股票失败不影响整体
            continue

    elapsed_total = time.time() - start_time
    logger.info("=" * 60)
    logger.info("股票数据采集全部完成: %d 只, 耗时 %.1f 分钟", total, elapsed_total / 60)
    logger.info("=" * 60)


# ═══════════════════════════════════════════════════════════════════════════
# 数据验证
# ═══════════════════════════════════════════════════════════════════════════
def validate_data(db_writer: StockDbWriter):
    """验证采集数据的完整性和质量。"""
    logger.info("=" * 60)
    logger.info("开始数据验证...")
    logger.info("=" * 60)

    with db_writer._conn() as conn:
        with conn.cursor() as cur:
            # 1. 每张表的行数
            tables = [
                "stock_basic", "stock_daily", "stock_daily_basic",
                "stock_income", "stock_balance_sheet", "stock_cashflow",
            ]
            logger.info("--- 各表行数 ---")
            for table in tables:
                cur.execute(f"SELECT COUNT(*) FROM {table}")
                count = cur.fetchone()[0]
                logger.info("  %-25s %d 行", table, count)

            # 2. 每只股票的 daily 行数
            cur.execute("""
                SELECT "TsCode", COUNT(*) as cnt
                FROM stock_daily
                GROUP BY "TsCode"
                ORDER BY cnt ASC
                LIMIT 5
            """)
            low_daily = cur.fetchall()
            if low_daily:
                logger.info("--- 日线行数最少的5只股票 ---")
                for code, cnt in low_daily:
                    logger.info("  %s: %d 行", code, cnt)

            # 3. NULL 值检查 - 利润表
            cur.execute("""
                SELECT
                    COUNT(*) FILTER (WHERE "NetProfit" IS NULL) as null_profit,
                    COUNT(*) FILTER (WHERE "Revenue" IS NULL) as null_revenue,
                    COUNT(*) as total
                FROM stock_income
            """)
            null_stats = cur.fetchone()
            if null_stats and null_stats[2] > 0:
                logger.info("--- 利润表 NULL 统计 ---")
                logger.info("  NetProfit: %d/%d (%.1f%%)",
                            null_stats[0], null_stats[2], null_stats[0] / null_stats[2] * 100)
                logger.info("  Revenue:   %d/%d (%.1f%%)",
                            null_stats[1], null_stats[2], null_stats[1] / null_stats[2] * 100)

            # 4. CapEx 字段检查
            cur.execute("""
                SELECT
                    COUNT(*) FILTER (WHERE "CapEx" IS NOT NULL) as has_capex,
                    COUNT(*) FILTER (WHERE "CapEx" IS NULL) as null_capex,
                    COUNT(*) as total
                FROM stock_cashflow
            """)
            capex_stats = cur.fetchone()
            if capex_stats and capex_stats[2] > 0:
                logger.info("--- CapEx 字段统计 ---")
                logger.info("  有数据: %d/%d (%.1f%%)",
                            capex_stats[0], capex_stats[2], capex_stats[0] / capex_stats[2] * 100)
                logger.info("  NULL:   %d/%d (%.1f%%)",
                            capex_stats[1], capex_stats[2], capex_stats[1] / capex_stats[2] * 100)

            # 5. 抽查：贵州茅台 600519.SH
            logger.info("--- 抽查: 贵州茅台 600519.SH ---")
            cur.execute("""SELECT * FROM stock_basic WHERE "TsCode" = '600519.SH'""")
            row = cur.fetchone()
            if row:
                logger.info("  stock_basic: %s", row)
            else:
                logger.warning("  stock_basic: 未找到!")

            for table, pk_cols in [
                ("stock_daily", '"TsCode", "TradeDate"'),
                ("stock_daily_basic", '"TsCode", "TradeDate"'),
                ("stock_income", '"TsCode", "EndDate", "ReportType"'),
                ("stock_balance_sheet", '"TsCode", "EndDate", "ReportType"'),
                ("stock_cashflow", '"TsCode", "EndDate", "ReportType"'),
            ]:
                cur.execute(f'SELECT COUNT(*) FROM {table} WHERE "TsCode" = %s', ('600519.SH',))
                cnt = cur.fetchone()[0]
                logger.info("  %-25s %d 行", table, cnt)

            # 6. 查看茅台最新一条现金流量表（验证 CapEx 有值）
            cur.execute("""
                SELECT "EndDate", "ReportType", "OperCashFlow", "CapEx"
                FROM stock_cashflow
                WHERE "TsCode" = '600519.SH'
                ORDER BY "EndDate" DESC
                LIMIT 3
            """)
            cf_rows = cur.fetchall()
            if cf_rows:
                logger.info("  茅台最新现金流量表:")
                for r in cf_rows:
                    logger.info("    EndDate=%s, ReportType=%s, OperCF=%s, CapEx=%s", *r)
            else:
                logger.warning("  茅台现金流量表: 无数据!")

    logger.info("=" * 60)
    logger.info("数据验证完成")
    logger.info("=" * 60)


# ═══════════════════════════════════════════════════════════════════════════
# 入口
# ═══════════════════════════════════════════════════════════════════════════
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="VolSurf 股票数据采集")
    parser.add_argument("--backfill", action="store_true", help="全量回填模式（拉3-5年历史）")
    parser.add_argument("--ts-code", type=str, default=None, help="只拉单只股票（调试用）")
    parser.add_argument("--limit", type=int, default=None, help="限制拉取股票数量（测试用）")
    parser.add_argument("--validate-only", action="store_true", help="只执行数据验证，不拉取数据")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )

    config = Config.load()

    # 从主机运行时覆盖 DB_HOST 为 localhost（.env 里是容器名 postgres）
    db_host = os.environ.get("DB_HOST_OVERRIDE", "localhost")

    fetcher = StockFetcher(config.tushare_token)
    db_writer = StockDbWriter(
        host=db_host, port=config.db_port, dbname=config.db_name,
        user=config.db_user, password=config.db_password,
    )

    if args.validate_only:
        validate_data(db_writer)
    else:
        run_fetch(
            fetcher, db_writer,
            backfill=args.backfill,
            ts_code=args.ts_code,
            limit=args.limit,
        )
        # 采集完成后自动验证
        validate_data(db_writer)
