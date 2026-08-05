// components/stock/StockOverview.tsx
// 公司概览 + 评分 + 预警横幅

import Link from "next/link";
import { ArrowLeft, AlertTriangle } from "lucide-react";
import type { StockAnalysisReport } from "@/lib/types";
import { ScoreRing } from "./ScoreBadge";

interface StockOverviewProps {
  data: StockAnalysisReport;
}

export function StockOverview({ data }: StockOverviewProps) {
  const { tsCode, name, industry, reportDate, warnings, market } = data;
  const hasWarnings = warnings.length > 0;

  return (
    <div className="space-y-3">
      {/* 预警横幅 */}
      {hasWarnings && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 dark:border-amber-800 dark:bg-amber-950/50 p-3">
          <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
          <div className="text-sm">
            <span className="font-medium text-amber-800 dark:text-amber-300">
              检测到 {warnings.length} 项风险预警
            </span>
            <p className="text-amber-700 dark:text-amber-400 mt-0.5">
              {warnings[0]?.message}
              {warnings.length > 1 && ` 等 ${warnings.length} 条`}
            </p>
          </div>
        </div>
      )}

      {/* 公司概览卡片 */}
      <div className="rounded-lg border bg-card p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <Link
                href="/stocks"
                className="text-muted-foreground hover:text-foreground transition-colors"
              >
                <ArrowLeft className="h-4 w-4" />
              </Link>
              <h1 className="text-2xl font-semibold">{name}</h1>
              <span className="font-mono text-sm text-muted-foreground">
                {tsCode}
              </span>
            </div>
            <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              {industry && (
                <Link
                  href={`/stocks?industry=${encodeURIComponent(industry)}`}
                  className="px-2 py-0.5 rounded bg-muted hover:bg-accent transition-colors"
                >
                  {industry}
                </Link>
              )}
              {reportDate && <span>报告期：{reportDate}</span>}
              {market.price > 0 && (
                <span>
                  现价：
                  <span className="font-semibold text-foreground tabular-nums">
                    {market.price.toFixed(2)}
                  </span>
                </span>
              )}
            </div>
          </div>

          {/* 评分区 */}
          <div className="flex items-center gap-4">
            <ScoreRing score={data.overallScore} label="综合" size={72} />
            <div className="grid grid-cols-3 gap-3">
              <ScoreMini score={data.healthScore} label="健康度" />
              <ScoreMini score={data.growthScore} label="成长性" />
              <ScoreMini score={data.valueScore} label="估值" />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function ScoreMini({ score, label }: { score: number; label: string }) {
  const color =
    score >= 80
      ? "text-emerald-600 dark:text-emerald-400"
      : score >= 60
      ? "text-blue-600 dark:text-blue-400"
      : score >= 40
      ? "text-amber-600 dark:text-amber-400"
      : "text-red-600 dark:text-red-400";

  return (
    <div className="flex flex-col items-center">
      <span className={`text-lg font-bold tabular-nums ${color}`}>
        {score.toFixed(1)}
      </span>
      <span className="text-xs text-muted-foreground">{label}</span>
    </div>
  );
}
