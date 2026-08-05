// components/stock/FinancialMetrics.tsx
// 区域3：财务指标
// - 近8季度趋势线图（营收/净利/ROE三线叠加，双Y轴）
// - 财务指标卡片网格（ROE/毛利率/净利率/负债率/营收增长/净利增长/OCF/FCF/商誉/应收）
// - 每个指标显示数值 + 同比变化（箭头）

"use client";

import { useMemo } from "react";
import { ArrowUp, ArrowDown, Minus } from "lucide-react";
import * as echarts from "echarts/core";
import { LineChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";
import type { FinancialMetrics, MetricItem } from "@/lib/types";

echarts.use([
  LineChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  CanvasRenderer,
]);

interface FinancialMetricsProps {
  financial: FinancialMetrics;
}

/** 生成近8季度的标签 Q1-Q4 */
function generateQuarterLabels(count: number): string[] {
  const labels: string[] = [];
  const now = new Date();
  let year = now.getFullYear();
  let q = Math.floor(now.getMonth() / 3) + 1; // 当前季度
  for (let i = 0; i < count; i++) {
    labels.unshift(`${year}Q${q}`);
    q--;
    if (q < 1) {
      q = 4;
      year--;
    }
  }
  return labels;
}

function TrendChart({
  revenueTrend,
  profitTrend,
  roeTrend,
}: {
  revenueTrend: number[];
  profitTrend: number[];
  roeTrend: number[];
}) {
  const option = useMemo(() => {
    const n = Math.max(revenueTrend.length, profitTrend.length, roeTrend.length);
    const labels = generateQuarterLabels(n);
    return {
      backgroundColor: "transparent",
      tooltip: {
        trigger: "axis" as const,
        axisPointer: { type: "cross" as const },
      },
      legend: {
        data: ["营收(亿)", "净利(亿)", "ROE(%)"],
        top: 0,
      },
      grid: { left: 60, right: 60, top: 40, bottom: 30 },
      xAxis: {
        type: "category" as const,
        data: labels,
        axisLabel: { fontSize: 11 },
      },
      yAxis: [
        {
          type: "value" as const,
          name: "金额(亿)",
          position: "left" as const,
          axisLabel: { fontSize: 11 },
        },
        {
          type: "value" as const,
          name: "ROE(%)",
          position: "right" as const,
          axisLabel: { fontSize: 11, formatter: "{value}%" },
        },
      ],
      series: [
        {
          name: "营收(亿)",
          type: "line" as const,
          yAxisIndex: 0,
          data: revenueTrend,
          smooth: true,
          symbol: "circle",
          symbolSize: 6,
          itemStyle: { color: "#5470c6" },
          lineStyle: { width: 2 },
        },
        {
          name: "净利(亿)",
          type: "line" as const,
          yAxisIndex: 0,
          data: profitTrend,
          smooth: true,
          symbol: "circle",
          symbolSize: 6,
          itemStyle: { color: "#91cc75" },
          lineStyle: { width: 2 },
        },
        {
          name: "ROE(%)",
          type: "line" as const,
          yAxisIndex: 1,
          data: roeTrend,
          smooth: true,
          symbol: "circle",
          symbolSize: 6,
          itemStyle: { color: "#ee6666" },
          lineStyle: { width: 2 },
        },
      ],
    };
  }, [revenueTrend, profitTrend, roeTrend]);

  return (
    <ReactECharts
      option={option}
      style={{ height: "300px", width: "100%" }}
      notMerge
      lazyUpdate
    />
  );
}

/** 单个财务指标卡片 */
function MetricCard({ item }: { item: MetricItem | null | undefined }) {
  if (!item) {
    return (
      <div className="rounded-lg border bg-card p-3 min-h-[88px] flex items-center justify-center text-muted-foreground text-sm">
        暂无数据
      </div>
    );
  }

  const hasYoY = item.yoyChange != null;
  const isUp = hasYoY && item.yoyChange! > 0;
  const isDown = hasYoY && item.yoyChange! < 0;
  const isFlat = hasYoY && item.yoyChange! === 0;

  const levelColor = (() => {
    switch (item.level) {
      case "excellent":
        return "text-green-600";
      case "good":
        return "text-blue-600";
      case "normal":
        return "text-yellow-600";
      case "warn":
        return "text-orange-600";
      case "danger":
        return "text-red-600";
      default:
        return "text-foreground";
    }
  })();

  return (
    <div className="rounded-lg border bg-card p-3 space-y-1">
      <div className="flex items-center justify-between">
        <span className="text-xs text-muted-foreground">{item.label}</span>
        {hasYoY && (
          <span
            className={`text-xs inline-flex items-center ${
              isUp
                ? "text-red-600"
                : isDown
                  ? "text-green-600"
                  : "text-muted-foreground"
            }`}
          >
            {isUp && <ArrowUp className="h-3 w-3" />}
            {isDown && <ArrowDown className="h-3 w-3" />}
            {isFlat && <Minus className="h-3 w-3" />}
            {Math.abs(item.yoyChange!).toFixed(2)}
            {item.unit === "%" ? "pp" : ""}
          </span>
        )}
      </div>
      <div className={`text-xl font-bold ${levelColor}`}>
        {item.value.toFixed(2)}
        <span className="text-sm font-normal text-muted-foreground ml-1">
          {item.unit}
        </span>
      </div>
      {item.description && (
        <p className="text-[10px] text-muted-foreground line-clamp-2">
          {item.description}
        </p>
      )}
    </div>
  );
}

export function FinancialMetrics({ financial }: FinancialMetricsProps) {
  const hasTrend =
    (financial.revenueTrend?.length ?? 0) > 0 ||
    (financial.profitTrend?.length ?? 0) > 0 ||
    (financial.roeTrend?.length ?? 0) > 0;

  const metrics: (MetricItem | null | undefined)[] = [
    financial.roe,
    financial.grossMargin,
    financial.netMargin,
    financial.debtRatio,
    financial.revenueGrowth,
    financial.profitGrowth,
    financial.ocfToProfit,
    financial.freeCashFlow,
    financial.goodwillRatio,
    financial.recvRatio,
  ];

  return (
    <div className="rounded-lg border bg-card p-4 space-y-4">
      <h3 className="text-lg font-semibold">财务指标</h3>

      {hasTrend && (
        <div>
          <h4 className="text-sm font-medium text-muted-foreground mb-2">
            近8季度趋势
          </h4>
          <TrendChart
            revenueTrend={financial.revenueTrend ?? []}
            profitTrend={financial.profitTrend ?? []}
            roeTrend={financial.roeTrend ?? []}
          />
        </div>
      )}

      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        {metrics.map((m, i) => (
          <MetricCard key={i} item={m} />
        ))}
      </div>
    </div>
  );
}
