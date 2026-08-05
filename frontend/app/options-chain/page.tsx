// app/options-chain/page.tsx
// 期权链页面（SSR）

import { Suspense } from "react";
import { api } from "@/lib/api";
import type { OptionChainResponse } from "@/lib/types";
import { OptionChainClient } from "./option-chain-client";

export const revalidate = 300; // 5 分钟缓存

interface PageProps {
  searchParams: { underlying?: string; expiry?: string; date?: string };
}

export default async function OptionChainPage({ searchParams }: PageProps) {
  const underlying = searchParams.underlying || "510050";
  const expiry = searchParams.expiry;
  const date = searchParams.date;

  // 如果没有指定 expiry，先不加载数据
  if (!expiry) {
    return (
      <div className="container py-6">
        <h1 className="text-2xl font-semibold mb-4">期权链</h1>
        <p className="text-muted-foreground text-sm">
          请先选择标的和到期月。
        </p>
        <Suspense fallback={null}>
          <OptionChainClient
            underlying={underlying}
            expiry={undefined}
            date={date}
            initialData={null}
          />
        </Suspense>
      </div>
    );
  }

  // SSR 预取期权链数据
  let initialData: OptionChainResponse | null = null;
  let errorMessage: string | null = null;
  try {
    initialData = await api.getOptionChain(underlying, expiry, date);
  } catch (err: any) {
    errorMessage = err?.message || "加载期权链数据失败";
  }

  return (
    <div className="container py-6 space-y-4">
      <h1 className="text-2xl font-semibold">期权链</h1>
      <Suspense fallback={null}>
        <OptionChainClient
          underlying={underlying}
          expiry={expiry}
          date={date}
          initialData={initialData}
          initialError={errorMessage}
        />
      </Suspense>
    </div>
  );
}
