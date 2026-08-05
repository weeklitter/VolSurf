// app/stocks/page.tsx
// 股票首页 - SSR 获取行业列表和初始股票列表

import { Suspense } from "react";
import { api, ApiError } from "@/lib/api";
import type { StockListResponse } from "@/lib/types";
import { StocksClient } from "./stocks-client";
import { Loading } from "@/components/common/Loading";

export const revalidate = 300;

interface PageProps {
  searchParams: { industry?: string; page?: string };
}

export default async function StocksPage({ searchParams }: PageProps) {
  const industry = searchParams.industry || undefined;
  const page = parseInt(searchParams.page || "1", 10);

  // SSR 并行获取行业列表和股票列表
  const [industriesResult, stocksResult] = await Promise.allSettled([
    api.getIndustries(),
    api.getStockList(industry, page, 20),
  ]);

  const industries =
    industriesResult.status === "fulfilled" ? industriesResult.value : [];
  const initialStocks: StockListResponse | null =
    stocksResult.status === "fulfilled" ? stocksResult.value : null;
  const initialError: string | null =
    stocksResult.status === "rejected"
      ? stocksResult.reason instanceof ApiError
        ? stocksResult.reason.message
        : "加载股票列表失败"
      : null;

  return (
    <div className="container py-6">
      <div className="mb-6">
        <h1 className="text-2xl font-semibold">股票分析</h1>
        <p className="text-sm text-muted-foreground mt-1">
          浏览 A 股个股基本面分析与估值评估
        </p>
      </div>

      <Suspense fallback={<Loading />}>
        <StocksClient
          industries={industries}
          initialStocks={initialStocks}
          initialError={initialError}
          initialIndustry={industry}
          initialPage={page}
        />
      </Suspense>
    </div>
  );
}
