// components/stock/ValuationChart.tsx
// 区域4：估值分位
// - PE/PB/PS 估值卡片（当前值 + 分位描述）
// - 股息率显示
// - 总市值显示
// - 如果有 percentile5y 数据，使用仪表盘；否则只用卡片展示

"use client";

import { useMemo } from "react";
import * as echarts from "echarts/core";
import { GaugeChart } from "echarts/charts";
import { TooltipComponent } from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";
import type { ValuationMetrics, ValuationItem } from "@/lib/types";

echarts.use([GaugeChart, TooltipComponent, CanvasRenderer]);

interface ValuationChartProps {
  valuation: ValuationMetrics;
}

const LEVEL_LABELS: Record<string, string> = {
  undervalued: "低估",
  fair: "合理",
  overvalued: "高估",
};

const LEVEL_COLORS: Record<string, string> = {
  undervalued: "text-green-600",
  fair: "text-blue-600",
  overvalued: "text-red-600",
};

/** 格式化市值（万元 -> 亿 / 万） */
function formatMarketCap(mv: number | undefined): string {
  if (mv == null) return "-";
  // 后端 totalMv 单位是万元
  if (mv >= 10000) return `${(mv / 10000).toFixed(2)} 亿`;
  return `${mv.toFixed(2)} 万`;
}

function ValuationCard({ item }: { item: ValuationItem | null | undefined }) {
  if (!item) {
    return (
      <div className="rounded-lg border bg-card p-4 flex items-center justify-center text-muted-foreground text-sm min-h-[100px]">
        暂无数据
      </div>
    );
  }

  const hasPercentile = item.percentile5y != null;
  const levelLabel = LEVEL_LABELS[item.level] ?? item.level;
  const levelColor = LEVEL_COLORS[item.level] ?? "text-foreground";

  return (
    <div className="rounded-lg border bg-card p-4 space-y-2">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-muted-foreground">
          {item.label}
        </span>
        <span className={`text-xs font-medium ${levelColor}`}>
          {levelLabel}
        </span>
      </div>
      <div className="text-2xl font-bold">{item.value.toFixed(2)}</div>
      {hasPercentile && (
        <div className="space-y-1">
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>5年分位</span>
            <span className={levelColor}>{item.percentile5y!.toFixed(1)}%</span>
          </div>
          <div className="w-full bg-muted rounded-full h-1.5 overflow-hidden">
            <div
              className={`h-full rounded-full ${
                item.percentile5y! < 30
                  ? "bg-green-500"
                  : item.percentile5y! < 70
                    ? "bg-blue-500"
                    : "bg-red-500"
              }`}
              style={{ width: `${Math.min(item.percentile5y!, 100)}%` }}
            />
          </div>
          {(item.min5y != null || item.max5y != null || item.median5y != null) && (
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>最低 {item.min5y?.toFixed(2) ?? "-"}</span>
              <span>中位 {item.median5y?.toFixed(2) ?? "-"}</span>
              <span>最高 {item.max5y?.toFixed(2) ?? "-"}</span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ValuationGauge({ item }: { item: ValuationItem }) {
  const option = useMemo(() => {
    const percentile = item.percentile5y ?? 0;
    return {
      backgroundColor: "transparent",
      series: [
        {
          type: "gauge" as const,
          startAngle: 200,
          endAngle: -20,
          min: 0,
          max: 100,
          splitNumber: 5,
          progress: {
            show: true,
            width: 12,
            itemStyle: {
              color:
                percentile < 30
                  ? "#91cc75"
                  : percentile < 70
                    ? "#5470c6"
                    : "#ee6666",
            },
          },
          axisLine: {
            lineStyle: {
              width: 12,
              color: [[1, "rgba(128,128,128,0.15)"]],
            },
          },
          pointer: { show: false },
          axisTick: { show: false },
          splitLine: { show: false },
          axisLabel: { show: false },
          detail: {
            valueAnimation: true,
            formatter: `{value}%`,
            fontSize: 18,
            offsetCenter: [0, "20%"],
          },
          title: {
            offsetCenter: [0, "60%"],
            fontSize: 12,
            color: "#888",
          },
          data: [{ value: percentile.toFixed(1), name: `${item.label} 5年分位` }],
        },
      ],
    };
  }, [item]);

  return (
    <ReactECharts
      option={option}
      style={{ height: "180px", width: "100%" }}
      notMerge
      lazyUpdate
    />
  );
}

export function ValuationChart({ valuation }: ValuationChartProps) {
  const items = [valuation.pe, valuation.pb, valuation.ps].filter(
    (v): v is ValuationItem => v != null
  );
  const hasPercentile = items.some((v) => v.percentile5y != null);

  const dvRatio = valuation.dvRatio;

  return (
    <div className="rounded-lg border bg-card p-4 space-y-4">
      <h3 className="text-lg font-semibold">估值分析</h3>

      {/* 估值卡片 */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <ValuationCard item={valuation.pe} />
        <ValuationCard item={valuation.pb} />
        <ValuationCard item={valuation.ps} />
      </div>

      {/* 如果有分位数据，展示仪表盘 */}
      {hasPercentile && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
          {items.map((item, i) => (
            <div key={i} className="rounded-lg border bg-background/50 p-2">
              <ValuationGauge item={item} />
            </div>
          ))}
        </div>
      )}

      {/* 股息率 + 总市值 */}
      <div className="grid grid-cols-2 gap-3">
        <div className="rounded-lg border bg-card p-4">
          <div className="text-sm text-muted-foreground mb-1">股息率</div>
          <div className="text-xl font-bold">
            {dvRatio?.value != null
              ? `${dvRatio.value.toFixed(2)}%`
              : "-"}
          </div>
          {dvRatio?.description && (
            <p className="text-[10px] text-muted-foreground mt-1">
              {dvRatio.description}
            </p>
          )}
        </div>
        <div className="rounded-lg border bg-card p-4">
          <div className="text-sm text-muted-foreground mb-1">总市值</div>
          <div className="text-xl font-bold">
            {formatMarketCap(valuation.totalMv)}
          </div>
        </div>
      </div>

      {/* 估值评分 */}
      <div className="flex items-center justify-between rounded-lg border bg-card p-4">
        <span className="text-sm text-muted-foreground">估值评分</span>
        <span className="text-lg font-bold text-green-600">
          {valuation.valueScore.toFixed(1)}
        </span>
      </div>
    </div>
  );
}
