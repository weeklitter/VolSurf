# VolSurf - 期权波动率分析工具

国内期权市场的 Web 端波动率分析平台：隐含波动率（IV）、Greeks、3D 波动率曲面、微笑曲线、期限结构，以及 A 股基本面分析。

> ⚠️ 本项目仅供学习研究，所有数据不构成投资建议。期权交易存在高风险，可能导致全部本金损失。

## ✨ 功能

- **期权链**：T 型报价，IV / Delta / Gamma / Theta / Vega / Rho
- **波动率曲面 3D**：行权价 × 到期时间 × IV 交互式可视化
- **波动率微笑曲线**：单到期月 IV vs 行权价 + Skew 指标
- **波动率期限结构**：ATM IV vs 到期时间（contango / backwardation）
- **IV 百分位**：当前 IV 在过去 252 个交易日的分位
- **个股分析**：沪深300 成分股基本面（三大报表）、估值（PE/PB 分位）、市场表现、13 条异常预警

## 🧱 技术栈

| 层 | 选型 |
|----|------|
| 前端 | Next.js 14 + TypeScript + TailwindCSS + Plotly.js / ECharts |
| 后端 | .NET 8 ASP.NET Core（B-S 模型、IV 求解、Greeks、规则引擎） |
| 数据采集 | Python FastAPI + Tushare / AKShare |
| 数据库 | PostgreSQL |

## 🚀 快速启动

```bash
# 1. 配置环境变量
cp .env.example .env          # 填入 TUSHARE_TOKEN / DB_PASSWORD / INTERNAL_KEY

# 2. 启动全栈
docker compose up -d

# 3. 访问
#   http://localhost:3000   (前端)
#   http://localhost:5000   (API /api/health)
```

开发模式（热重载）：

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d postgres data-fetcher
cd frontend && npm install && npm run dev
cd src/VolSurf.Api && dotnet run
```

## 🔐 环境变量

见各目录 `.env.example`：根目录（docker compose）、`data-fetcher/`、`frontend/`。

| 变量 | 说明 |
|------|------|
| `TUSHARE_TOKEN` | Tushare Pro token（[tushare.pro](https://tushare.pro) 注册获取） |
| `DB_PASSWORD` | PostgreSQL 密码 |
| `INTERNAL_KEY` | data-fetcher → API 内部通知密钥 |
| `NEXT_PUBLIC_API_BASE_URL` | 前端 API 地址 |

> 敏感信息一律通过环境变量注入，**不要提交真实值**。

## 🗂 目录结构

```
VolSurf/
├── src/
│   ├── VolSurf.Api/     # .NET 8 Web API
│   ├── VolSurf.Core/    # B-S 模型 / IV 求解 / Greeks / 分析引擎
│   └── VolSurf.Data/    # EF Core + PostgreSQL
├── data-fetcher/        # Python 定时数据采集（Tushare/AKShare）
├── frontend/            # Next.js 前端
└── nginx/               # 反向代理配置
```

## 📊 数据源

- **Tushare Pro**：期权日线、合约信息、股票财务（需积分）
- **AKShare**：备用数据源、标的行情

## 🧪 合规说明

- 不做交易执行、不做荐股/投资建议、不做策略收益建议
- 数据仅供学习研究，盈亏由使用者自行承担

## 📄 License

[MIT](LICENSE) © weeklitter
