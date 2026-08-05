// lib/types.ts
// 完整的 TypeScript 类型定义 - 对应后端 API 响应

// ── 通用响应包装 ──
export interface ApiResponse<T> {
  code: number;
  data: T | null;
  message: string;
  error?: {
    type: string;
    timestamp: string;
  };
}

// ── 标的 ──
export interface Underlying {
  tsCode: string;
  name: string;
  exchange: string;
  assetClass: string;
}

export interface UnderlyingWithPrice extends Underlying {
  price: number;
}

// ── 交易日 / 到期月 ──
export type TradeDate = string; // YYYY-MM-DD
export type Expiry = string; // YYYY-MM-DD

// ── 期权合约数据（统一结构） ──
export interface OptionContractData {
  tsCode: string;
  strike: number;
  price: number | null;
  settle: number | null;
  volume: number;
  openInterest: number;
  iv: number | null;
  delta: number | null;
  ivConfidence: boolean;
}

// ── 期权链响应 ──
export interface OptionChainResponse {
  underlying: {
    tsCode: string;
    name: string;
    price: number;
  };
  tradeDate: string;
  expiry: string;
  ivPercentile: number | null;
  calls: OptionContractData[];
  puts: OptionContractData[];
}

// ── 波动率曲面 ──
export interface VolSurfacePoint {
  moneyness: number; // S/K
  timeToExpiry: number; // 年化
  iv: number;
  strike: number;
  expiry: string; // YYYY-MM-DD
  callPut: "C" | "P";
}

export interface VolSurfaceResponse {
  underlying: string;
  underlyingPrice: number;
  date: string;
  expiries: string[];
  points: VolSurfacePoint[];
}

// ── 微笑曲线 ──
export interface SmilePoint {
  strike: number;
  iv: number;
  delta: number;
}

export interface VolSmileResponse {
  underlying: string;
  expiry: string;
  date: string;
  atmIv: number;
  skew25: number;
  calls: SmilePoint[];
  puts: SmilePoint[];
}

// ── 期限结构 ──
export interface TermStructurePoint {
  expiry: string;
  daysToExpiry: number;
  atmIv: number;
}

export interface TermStructureResponse {
  underlying: string;
  date: string;
  points: TermStructurePoint[];
}

// ── IV 百分位 ──
export interface IvPercentileResponse {
  underlying: string;
  tradeDate: string;
  atmIv: number;
  ivPercentile: number;
  ivMean: number;
  ivStd: number;
  sampleDays: number;
}

// ── T 型报价表行（合并 call/put） ──
export interface OptionChainRow {
  strike: number;
  call: OptionContractData;
  put: OptionContractData;
}

// ── 内部接口 ──
export interface TriggerCalcRequest {
  tradeDate: string;
}

export interface TriggerCalcResponse {
  tradeDate: string;
  status: "queued" | "running" | "completed" | "failed";
  totalContracts?: number;
  calculated?: number;
  skipped?: number;
  anomalies?: number;
  duration?: string;
}

export interface CalcStatusResponse {
  tradeDate: string;
  status: TriggerCalcResponse["status"];
}


// ════════════════════════════════════════════════════════════
// 股票模块类型
// ════════════════════════════════════════════════════════════

// ── 股票搜索结果 ──
export interface StockSearchResult {
  tsCode: string;
  symbol: string;
  name: string;
  industry: string | null;
  market: string | null;
}

// ── 股票列表响应 ──
export interface StockListResponse {
  stocks: StockSearchResult[];
  total: number;
  page: number;
  size: number;
}

// ── 指标项 ──
export interface MetricItem {
  value: number;
  prevYear?: number | null;
  yoyChange?: number | null;
  trend: "up" | "down" | "stable";
  score: number;
  level: "excellent" | "good" | "normal" | "warn" | "danger";
  label: string;
  unit: string;
  description?: string;
}

// ── 估值项 ──
export interface ValuationItem {
  value: number;
  percentile5y?: number;
  median5y?: number;
  min5y?: number;
  max5y?: number;
  level: "undervalued" | "fair" | "overvalued";
  label: string;
}

// ── 财务指标 ──
export interface FinancialMetrics {
  roe?: MetricItem | null;
  roa?: MetricItem | null;
  grossMargin?: MetricItem | null;
  netMargin?: MetricItem | null;
  debtRatio?: MetricItem | null;
  revenueGrowth?: MetricItem | null;
  profitGrowth?: MetricItem | null;
  ocfToProfit?: MetricItem | null;
  freeCashFlow?: MetricItem | null;
  goodwillRatio?: MetricItem | null;
  recvRatio?: MetricItem | null;
  revenueTrend: number[];
  profitTrend: number[];
  roeTrend: number[];
  healthScore: number;
  growthScore: number;
}

// ── 估值指标 ──
export interface ValuationMetrics {
  pe?: ValuationItem | null;
  peTtm?: ValuationItem | null;
  pb?: ValuationItem | null;
  ps?: ValuationItem | null;
  totalMv?: number;
  dvRatio?: MetricItem | null;
  valueScore: number;
}

// ── 市场表现 ──
export interface PricePoint {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  ma20?: number | null;
  ma60?: number | null;
  ma120?: number | null;
}

export interface MarketMetrics {
  price: number;
  pctChg1M: number;
  pctChg3M: number;
  pctChg1Y: number;
  pctChgYTD: number;
  vsHs3001Y: number;
  ma20: number;
  ma60: number;
  ma120: number;
  ma250: number;
  maTrend: "bull" | "bear" | "mixed";
  volatility: number;
  priceTrend: PricePoint[];
}

// ── 主营业务构成 ──
export interface BusinessItem {
  name: string;
  revenue: number;
  cost?: number;
  profit?: number;
  ratio?: number;
  margin?: number;
}

export interface BusinessComposition {
  byProduct: BusinessItem[];
  byRegion: BusinessItem[];
  revenueTrend5y: number[];
  endDate: string;
}

// ── 预警 ──
export interface Warning {
  type: string;
  level: "info" | "warn" | "danger";
  message: string;
  value: number;
  threshold: number;
}

// ── AI分析结果（预留）──
export interface AiAnalysisResult {
  summary: string;
  strengths: string[];
  risks: string[];
}

// ── 股票分析报告（核心结构）──
export interface StockAnalysisReport {
  tsCode: string;
  name: string;
  industry: string;
  reportDate: string;
  financial: FinancialMetrics;
  valuation: ValuationMetrics;
  market: MarketMetrics;
  business: BusinessComposition;
  warnings: Warning[];
  healthScore: number;
  growthScore: number;
  valueScore: number;
  overallScore: number;
  aiAnalysis?: AiAnalysisResult | null;
}

// ── K线数据 ──
export interface StockDailyData {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  pctChg?: number;
}
