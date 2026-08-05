// lib/api.ts
// 完整 API 调用层
// - 统一 ApiError
// - 统一 request() 封装（支持 SSR 缓存 + CSR）
// - 7 个业务接口

import type {
  Underlying,
  OptionChainResponse,
  VolSurfaceResponse,
  VolSmileResponse,
  TermStructureResponse,
  IvPercentileResponse,
  TriggerCalcResponse,
  CalcStatusResponse,
} from "./types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL || "/api";

// ── 统一错误类型 ──
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string | null,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// ── 内部类型（不导出 request<T> 泛型之外的细节） ──
interface RequestOptions {
  /** true=SSR（带 revalidate），false=CSR（默认） */
  ssr?: boolean;
  /** SSR 缓存时间（秒），默认 300 */
  revalidate?: number;
  /** 自定义 headers */
  headers?: Record<string, string>;
  /** AbortSignal */
  signal?: AbortSignal;
}

// ── 统一请求函数 ──
/**
 * 统一 API 请求函数
 *
 * SSR 策略：服务端渲染时使用 fetch + next: { revalidate: 300 }（5分钟缓存）
 * CSR 策略：客户端交互时使用普通 fetch（动态数据）
 *
 * @param path API 路径（不含 base URL）
 * @param options fetch 选项
 */
async function request<T>(
  path: string,
  options?: RequestOptions
): Promise<T> {
  const ssr = options?.ssr ?? false;
  const revalidate = options?.revalidate ?? 300;

  const fetchOptions: RequestInit & { next?: { revalidate: number } } = ssr
    ? { next: { revalidate }, headers: options?.headers }
    : { cache: "no-store", headers: options?.headers };

  if (options?.signal) {
    fetchOptions.signal = options.signal;
  }

  const res = await fetch(`${API_BASE}${path}`, fetchOptions);

  if (!res.ok) {
    const error = await res
      .json()
      .catch(() => ({ message: res.statusText, code: res.status }));
    throw new ApiError(
      res.status,
      error.error?.type ?? null,
      error.message || `API Error: ${res.status}`
    );
  }

  const json = (await res.json()) as {
    code: number;
    data: T | null;
    message: string;
    error?: { type: string; timestamp: string };
  };

  // 统一响应格式：{ code, data, message }
  if (json.code !== 200 && json.code !== 202) {
    throw new ApiError(
      json.code,
      json.error?.type ?? null,
      json.message || "Unknown error"
    );
  }

  return json.data as T;
}

// ── 业务方法 ──
export const api = {
  // ── 1. 获取标的列表（SSR，缓存 5 分钟） ──
  getUnderlyings: () =>
    request<Underlying[]>("/underlyings", { ssr: true, revalidate: 300 }),

  // ── 2. 获取可用交易日列表（SSR） ──
  getTradeDates: (underlying: string, limit: number = 30) =>
    request<string[]>(
      `/trade-dates?underlying=${encodeURIComponent(underlying)}&limit=${limit}`,
      { ssr: true, revalidate: 300 }
    ),

  // ── 3. 获取可用到期月列表（SSR） ──
  getExpiries: (underlying: string, date?: string) =>
    request<string[]>(
      `/expiries?underlying=${encodeURIComponent(underlying)}${
        date ? `&date=${encodeURIComponent(date)}` : ""
      }`,
      { ssr: true, revalidate: 300 }
    ),

  // ── 4. 获取期权链（SSR，缓存 5 分钟） ──
  getOptionChain: (underlying: string, expiry: string, date?: string) =>
    request<OptionChainResponse>(
      `/option-chain?underlying=${encodeURIComponent(underlying)}&expiry=${encodeURIComponent(
        expiry
      )}${date ? `&date=${encodeURIComponent(date)}` : ""}`,
      { ssr: true, revalidate: 300 }
    ),

  // ── 5. 获取波动率曲面（CSR） ──
  getVolSurface: (underlying: string, date?: string) =>
    request<VolSurfaceResponse>(
      `/vol-surface?underlying=${encodeURIComponent(underlying)}${
        date ? `&date=${encodeURIComponent(date)}` : ""
      }`,
      { ssr: false }
    ),

  // ── 6. 获取微笑曲线（CSR） ──
  getVolSmile: (underlying: string, expiry: string, date?: string) =>
    request<VolSmileResponse>(
      `/vol-smile?underlying=${encodeURIComponent(underlying)}&expiry=${encodeURIComponent(
        expiry
      )}${date ? `&date=${encodeURIComponent(date)}` : ""}`,
      { ssr: false }
    ),

  // ── 7. 获取期限结构（CSR） ──
  getTermStructure: (underlying: string, date?: string) =>
    request<TermStructureResponse>(
      `/term-structure?underlying=${encodeURIComponent(underlying)}${
        date ? `&date=${encodeURIComponent(date)}` : ""
      }`,
      { ssr: false }
    ),

  // ── 8. 获取 IV 百分位（SSR，缓存 5 分钟） ──
  getIvPercentile: (underlying: string) =>
    request<IvPercentileResponse>(
      `/iv-percentile?underlying=${encodeURIComponent(underlying)}`,
      { ssr: true, revalidate: 300 }
    ),

  // ── 9. 触发计算（内部接口） ──
  triggerCalc: async (
    tradeDate: string,
    internalKey: string
  ): Promise<TriggerCalcResponse> => {
    const res = await fetch(`${API_BASE}/internal/trigger-calc`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Internal-Key": internalKey,
      },
      body: JSON.stringify({ tradeDate }),
    });
    if (!res.ok) {
      const err = await res
        .json()
        .catch(() => ({ message: res.statusText }));
      throw new ApiError(
        res.status,
        err.error?.type ?? null,
        err.message || `API Error: ${res.status}`
      );
    }
    const json = await res.json();
    return json.data as TriggerCalcResponse;
  },

  // ── 10. 查询计算状态（内部接口） ──
  getCalcStatus: async (
    tradeDate: string,
    internalKey: string
  ): Promise<CalcStatusResponse> => {
    const res = await fetch(
      `${API_BASE}/internal/calc-status?tradeDate=${encodeURIComponent(
        tradeDate
      )}`,
      {
        headers: { "X-Internal-Key": internalKey },
        cache: "no-store",
      }
    );
    if (!res.ok) {
      const err = await res
        .json()
        .catch(() => ({ message: res.statusText }));
      throw new ApiError(
        res.status,
        err.error?.type ?? null,
        err.message || `API Error: ${res.status}`
      );
    }
    const json = await res.json();
    return json.data as CalcStatusResponse;
  },
};

// 导出 request 以供高级用法（自定义 hook 等）
export { request };
