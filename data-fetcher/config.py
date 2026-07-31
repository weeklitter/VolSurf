"""配置加载：从环境变量读取。

环境变量（与 .env.example 对应）：
  TUSHARE_TOKEN  Tushare Pro API Token
  DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD
  API_BASE_URL   .NET API 地址（容器内为 http://api:5000）
  INTERNAL_KEY   与 .NET 端 InternalKey:Key 共享
  LOG_LEVEL      DEBUG/INFO/WARNING，默认 INFO
"""
from __future__ import annotations

import logging
import os
from dataclasses import dataclass
from pathlib import Path


def _load_dotenv() -> None:
    """轻量 .env 加载：避免强依赖 pydantic-settings 在某些环境下解析失败。"""
    env_path = Path(__file__).resolve().parent / ".env"
    if not env_path.exists():
        return
    try:
        for raw_line in env_path.read_text(encoding="utf-8").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            if "=" not in line:
                continue
            key, _, value = line.partition("=")
            key = key.strip()
            value = value.strip().strip('"').strip("'")
            if key and key not in os.environ:
                os.environ[key] = value
    except Exception:  # noqa: BLE001
        pass


_load_dotenv()


@dataclass
class Config:
    tushare_token: str
    db_host: str
    db_port: int
    db_name: str
    db_user: str
    db_password: str
    api_base_url: str
    internal_key: str
    log_level: str

    @classmethod
    def load(cls) -> "Config":
        log_level = os.environ.get("LOG_LEVEL", "INFO").upper()
        level = getattr(logging, log_level, logging.INFO)
        logging.basicConfig(
            level=level,
            format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S",
        )

        return cls(
            tushare_token=os.environ.get("TUSHARE_TOKEN", ""),
            db_host=os.environ.get("DB_HOST", "postgres"),
            db_port=int(os.environ.get("DB_PORT", "5432")),
            db_name=os.environ.get("DB_NAME", "volsurf"),
            db_user=os.environ.get("DB_USER", "volsurf"),
            db_password=os.environ.get("DB_PASSWORD", ""),
            api_base_url=os.environ.get("API_BASE_URL", "http://api:5000"),
            internal_key=os.environ.get("INTERNAL_KEY", ""),
            log_level=log_level,
        )