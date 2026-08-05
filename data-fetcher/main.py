"""VolSurf data-fetcher 入口。

提供：
  - POST /trigger/manual?trade_date=YYYYMMDD   手动触发日终更新
  - GET  /trigger/status?trade_date=YYYYMMDD   查询 .NET 计算状态
  - GET  /health                                健康检查

启动时（lifespan）：注册 APScheduler 在每日 17:30（Asia/Shanghai）自动执行。
"""
from __future__ import annotations

import asyncio
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Query

from config import Config
from db.writer import DbWriter
from fetchers.akshare_fetcher import AkshareFetcher
from fetchers.tushare_fetcher import TushareFetcher
from notify.api_notifier import ApiNotifier
from scheduler import DailyUpdateScheduler

logger = logging.getLogger(__name__)


# ═══════════════════════════════════════════════════════════════════════════
# 启动时单例装配
# ═══════════════════════════════════════════════════════════════════════════
config = Config.load()

fetcher = TushareFetcher(config.tushare_token)
akshare_fetcher = AkshareFetcher()
db_writer = DbWriter(
    host=config.db_host,
    port=config.db_port,
    dbname=config.db_name,
    user=config.db_user,
    password=config.db_password,
)
api_notifier = ApiNotifier(config.api_base_url, config.internal_key)
scheduler = DailyUpdateScheduler(
    fetcher=fetcher,
    db_writer=db_writer,
    api_notifier=api_notifier,
    akshare_fetcher=akshare_fetcher,
)


# ═══════════════════════════════════════════════════════════════════════════
# 应用生命周期
# ═══════════════════════════════════════════════════════════════════════════
@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("VolSurf data-fetcher starting")
    scheduler.start()
    try:
        yield
    finally:
        scheduler.stop()
        logger.info("VolSurf data-fetcher stopped")


app = FastAPI(
    title="VolSurf Data Fetcher",
    description="期权数据拉取 + 入库 + 通知 .NET 计算",
    version="1.0.0",
    lifespan=lifespan,
)


# ═══════════════════════════════════════════════════════════════════════════
# 健康检查
# ═══════════════════════════════════════════════════════════════════════════
@app.get("/health")
async def health():
    return {"status": "ok", "service": "volsurf-data-fetcher"}


# ═══════════════════════════════════════════════════════════════════════════
# 手动触发日终更新（开发 / 调试用）
# ═══════════════════════════════════════════════════════════════════════════
@app.post("/trigger/manual")
async def manual_trigger(
    trade_date: str | None = Query(None, description="YYYYMMDD；缺省取今日"),
):
    """手动触发一次日终更新。

    异步返回 202，调度逻辑由 DailyUpdateScheduler.run_now 内部执行。
    失败重试与告警都在 scheduler 内部处理，本接口只负责入队。
    """
    if trade_date is not None and (len(trade_date) != 8 or not trade_date.isdigit()):
        raise HTTPException(status_code=400, detail="trade_date 必须为 YYYYMMDD")

    asyncio.create_task(scheduler.run_now(trade_date))
    return {
        "status": "accepted",
        "trade_date": trade_date or "today",
        "message": "任务已加入执行队列",
    }


# ═══════════════════════════════════════════════════════════════════════════
# 计算状态查询
# ═══════════════════════════════════════════════════════════════════════════
@app.get("/trigger/status")
async def get_status(trade_date: str = Query(..., description="YYYYMMDD")):
    if len(trade_date) != 8 or not trade_date.isdigit():
        raise HTTPException(status_code=400, detail="trade_date 必须为 YYYYMMDD")
    try:
        return await api_notifier.get_calc_status(trade_date)
    except Exception as exc:  # noqa: BLE001
        logger.error("查询计算状态失败: %s", exc)
        raise HTTPException(status_code=502, detail=f"上游错误: {exc}")
