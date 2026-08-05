// app/options-chain/option-chain-client.tsx
// 客户端组件：selector + table + 标的信息

"use client";

import { useQuery } from "@tanstack/react-query";
import { UnderlyingSelector } from "@/components/option-chain/UnderlyingSelector";
import { ExpirySelector } from "@/components/option-chain/ExpirySelector";
import { OptionChainTable } from "@/components/option-chain/OptionChainTable";
import { IvPercentileBadge } from "@/components/common/IvPercentileBadge";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import { api, ApiError } from "@/lib/api";
import { formatPrice } from "@/lib/utils";
import type { OptionChainResponse } from "@/lib/types";

interface OptionChainClientProps {
  underlying: string;
  expiry: string | undefined;
  date?: string;
  initialData?: OptionChainResponse | null;
  initialError?: string | null;
}

export function OptionChainClient({
  underlying,
  expiry,
  date,
  initialData = null,
  initialError = null,
}: OptionChainClientProps) {
  // 当 URL 变化时重新获取
  const { data, error, isLoading, refetch } = useQuery({
    queryKey: ["option-chain", underlying, expiry, date],
    queryFn: () => {
      if (!expiry) return Promise.resolve(null);
      return api.getOptionChain(underlying, expiry, date);
    },
    initialData: initialData || undefined,
    enabled: !!expiry,
  });

  // IV 百分位（独立查询）
  const { data: ivPercentile } = useQuery({
    queryKey: ["iv-percentile", underlying],
    queryFn: () => api.getIvPercentile(underlying),
    staleTime: 60 * 1000,
  });

  return (
    <div className="space-y-4">
      {/* 选择器 */}
      <div className="flex flex-wrap items-center gap-3 p-4 rounded-lg border bg-card">
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">标的：</span>
          <UnderlyingSelector />
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">到期：</span>
          <ExpirySelector underlying={underlying} date={date} />
        </div>
        {ivPercentile && (
          <IvPercentileBadge
            percentile={ivPercentile.ivPercentile}
            atmIv={ivPercentile.atmIv}
            className="ml-auto"
          />
        )}
      </div>

      {/* 内容 */}
      {!expiry ? (
        <div className="rounded-lg border bg-muted/30 p-12 text-center text-muted-foreground">
          请先选择到期月查看期权链
        </div>
      ) : isLoading ? (
        <Loading message="加载期权链..." />
      ) : error ? (
        <ErrorState
          message={(error as ApiError).message || initialError || "加载失败"}
          onRetry={() => refetch()}
        />
      ) : data ? (
        <>
          {/* 标的信息 */}
          <div className="rounded-lg border bg-card p-4">
            <div className="flex flex-wrap items-baseline gap-x-6 gap-y-2">
              <div>
                <span className="text-sm text-muted-foreground">品种：</span>
                <span className="text-lg font-semibold">
                  {data.underlying.name}
                </span>
                <span className="text-sm text-muted-foreground ml-2">
                  {data.underlying.tsCode}
                </span>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">现价：</span>
                <span className="text-lg font-semibold tabular-nums">
                  {formatPrice(data.underlying.price)}
                </span>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">交易日：</span>
                <span className="text-sm font-medium">{data.tradeDate}</span>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">到期日：</span>
                <span className="text-sm font-medium">{data.expiry}</span>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">合约数：</span>
                <span className="text-sm font-medium">
                  Call {data.calls.length} · Put {data.puts.length}
                </span>
              </div>
            </div>
          </div>

          {/* T 型报价表 */}
          <OptionChainTable data={data} />
        </>
      ) : (
        <ErrorState message={initialError || "暂无数据"} />
      )}
    </div>
  );
}
