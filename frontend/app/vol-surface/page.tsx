// app/vol-surface/page.tsx
// 波动率分析页面（CSR）

"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { VolSurfaceTabs } from "@/components/vol-surface/VolSurfaceTabs";
import { UnderlyingSelector } from "@/components/option-chain/UnderlyingSelector";
import { ExpirySelector } from "@/components/option-chain/ExpirySelector";
import { IvPercentileBadge } from "@/components/common/IvPercentileBadge";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

export default function VolSurfacePage() {
  const params = useSearchParams();
  const underlying = params.get("underlying") || "510050";
  const date = params.get("date") || undefined;
  const selectedExpiry = params.get("expiry") || undefined;

  // IV 百分位（SSR-friendly, 客户端获取）
  const { data: ivPercentile } = useQuery({
    queryKey: ["iv-percentile", underlying],
    queryFn: () => api.getIvPercentile(underlying),
    staleTime: 60 * 1000,
  });

  return (
    <div className="container py-6 space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">波动率分析</h1>
        {ivPercentile && (
          <IvPercentileBadge
            percentile={ivPercentile.ivPercentile}
            atmIv={ivPercentile.atmIv}
          />
        )}
      </div>

      {/* 选择器 */}
      <div className="flex flex-wrap items-center gap-3 p-4 rounded-lg border bg-card">
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">标的：</span>
          <UnderlyingSelector />
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">到期月：</span>
          <ExpirySelector underlying={underlying} date={date} />
        </div>
      </div>

      {/* 主图表区（3D / 微笑 / 期限） */}
      <VolSurfaceTabs
        underlying={underlying}
        date={date}
        selectedExpiry={selectedExpiry}
        expiries={[]}
      />
    </div>
  );
}
