"""调用 .NET 内部接口：触发 IV/Greeks 计算。"""
from __future__ import annotations

import asyncio
import logging
from datetime import datetime

import httpx

logger = logging.getLogger(__name__)


class ApiNotifier:
    def __init__(self, base_url: str, internal_key: str):
        self.base_url = base_url.rstrip("/")
        self.internal_key = internal_key

    async def trigger_calc(self, trade_date: str, max_retries: int = 3) -> dict:
        """通知 .NET 后端触发计算，带重试。

        Args:
            trade_date: YYYYMMDD 格式
            max_retries: 失败重试次数
        Returns:
            API 返回 JSON（dict）
        """
        url = f"{self.base_url}/api/internal/trigger-calc"
        headers = {
            "Content-Type": "application/json",
            "X-Internal-Key": self.internal_key,
        }
        # YYYYMMDD → YYYY-MM-DD
        date_iso = datetime.strptime(trade_date, "%Y%m%d").strftime("%Y-%m-%d")
        payload = {"tradeDate": date_iso}

        last_exc: Exception | None = None
        for attempt in range(1, max_retries + 1):
            try:
                async with httpx.AsyncClient(timeout=30) as client:
                    resp = await client.post(url, json=payload, headers=headers)

                if resp.status_code in (200, 202):
                    logger.info("trigger-calc 成功: status=%d body=%s",
                                resp.status_code, resp.text[:200])
                    return resp.json()

                if resp.status_code == 401:
                    logger.error("Internal key 鉴权失败: %s", resp.text)
                    raise RuntimeError("Internal key authentication failed")

                logger.warning("trigger-calc 返回 %d: %s", resp.status_code, resp.text)

            except httpx.TimeoutException as exc:
                last_exc = exc
                logger.warning("trigger-calc 超时 attempt=%d/%d", attempt, max_retries)
            except Exception as exc:  # noqa: BLE001
                last_exc = exc
                logger.error("trigger-calc 失败 attempt=%d/%d: %s", attempt, max_retries, exc)

            if attempt < max_retries:
                await asyncio.sleep(10)

        raise RuntimeError(f"trigger-calc 失败 {max_retries} 次: {last_exc}")

    async def get_calc_status(self, trade_date: str) -> dict:
        """查询指定交易日计算状态。"""
        url = f"{self.base_url}/api/internal/calc-status"
        headers = {"X-Internal-Key": self.internal_key}
        date_iso = datetime.strptime(trade_date, "%Y%m%d").strftime("%Y-%m-%d")
        params = {"tradeDate": date_iso}

        async with httpx.AsyncClient(timeout=15) as client:
            resp = await client.get(url, headers=headers, params=params)
            resp.raise_for_status()
            return resp.json()