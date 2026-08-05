// app/stocks/[tsCode]/page.tsx
// 个股分析页 - SSR 获取分析数据，渲染页面骨架

import { Suspense } from "react";
import { notFound } from "next/navigation";
import dynamic from "next/dynamic";
import { api, ApiError } from "@/lib/api";
import type { StockAnalysisReport } from "@/lib/types";
import { StockOverview } from "@/components/stock/StockOverview";
import { BusinessChart } from "@/components/stock/BusinessChart";
import { FinancialMetrics } from "@/components/stock/FinancialMetrics";
import { ValuationChart } from "@/components/stock/ValuationChart";
import { WarningList } from "@/components/stock/WarningList";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";

// K线图客户端懒加载（CSR only）
const StockKLine = dynamic(
  () => import("@/components/stock/StockKLine").then((m) => m.StockKLine),
  {
    ssr: false,
    loading: () => <Loading message="加载K线组件..." />,
  }
);

export const revalidate = 300; // 5 分钟缓存

interface PageProps {
  params: { tsCode: string };
}

export default async function StockAnalysisPage({ params }: PageProps) {
  const { tsCode } = params;

  // SSR 获取分析数据
  let analysis: StockAnalysisReport | null = null;
  let errorMessage: string | null = null;

  try {
    analysis = await api.getStockAnalysis(tsCode);
  } catch (err) {
    if (err instanceof ApiError) {
      if (err.status === 404) {
        notFound();
      }
      errorMessage = err.message;
    } else {
      errorMessage = "加载分析数据失败";
    }
  }

  if (errorMessage || !analysis) {
    return (
      <div className="container py-6">
        <ErrorState message={errorMessage || "暂无数据"} />
      </div>
    );
  }

  return (
    <div className="container py-6 space-y-4">
      {/* 区域1：公司概览 + 评分 + 预警横幅 */}
      <StockOverview data={analysis} />

      {/* 区域2：主营构成 */}
      <Suspense fallback={<Loading message="加载主营构成..." />}>
        <BusinessChart business={analysis.business} />
      </Suspense>

      {/* 区域3：财务指标 */}
      <Suspense fallback={<Loading message="加载财务指标..." />}>
        <FinancialMetrics financial={analysis.financial} />
      </Suspense>

      {/* 区域4：估值分位 */}
      <Suspense fallback={<Loading message="加载估值数据..." />}>
        <ValuationChart valuation={analysis.valuation} />
      </Suspense>

      {/* 区域5：K线图（CSR 懒加载） */}
      <Suspense fallback={<Loading message="加载K线图..." />}>
        <StockKLine tsCode={tsCode} />
      </Suspense>

      {/* 区域6：预警列表 */}
      <WarningList warnings={analysis.warnings} />
    </div>
  );
}
