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
