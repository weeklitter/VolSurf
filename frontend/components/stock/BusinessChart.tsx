// components/stock/BusinessChart.tsx
// 区域2：主营构成
// - 按产品/按地区切换按钮
// - 饼图（环形）显示主营构成
// - 近5年营收柱状图
// - 如果 byProduct 和 byRegion 都为空，显示"暂无主营业务构成数据"

"use client";

import { useState, useMemo } from "react";
import * as echarts from "echarts/core";
import { PieChart, BarChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";
import type { BusinessComposition } from "@/lib/types";

echarts.use([
  PieChart,
  BarChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  CanvasRenderer,
]);

interface BusinessChartProps {
  business: BusinessComposition;
}

const PIE_COLORS = [
  "#5470c6",
  "#91cc75",
  "#fac858",
  "#ee6666",
  "#73c0de",
  "#3ba272",
  "#fc8452",
  "#9a60b4",
  "#ea7ccc",
  "#5ab1ef",
  "#d87c7c",
  "#8d98b3",
];

export function BusinessChart({ business }: BusinessChartProps) {
  const hasProduct = (business.byProduct ?? []).length > 0;
  const hasRegion = (business.byRegion ?? []).length > 0;
  const hasAny = hasProduct || hasRegion;

  const [tab, setTab] = useState<"product" | "region">(
    hasProduct ? "product" : "region"
  );

  const currentData = useMemo(() => {
    const items = tab === "product" ? business.byProduct : business.byRegion;
    return (items ?? []).filter((item) => item.revenue > 0);
  }, [tab, business]);

  const pieOption = useMemo(() => {
    return {
      backgroundColor: "transparent",
      tooltip: {
        trigger: "item" as const,
        formatter: (params: any) => {
          return `${params.name}<br/>营收: ${params.value.toFixed(2)} 亿<br/>占比: ${params.percent}%`;
        },
      },
      legend: {
        type: "scroll" as const,
        bottom: 0,
        left: "center",
      },
      color: PIE_COLORS,
      series: [
        {
          name: tab === "product" ? "产品营收" : "地区营收",
          type: "pie" as const,
          radius: ["40%", "70%"],
          center: ["50%", "45%"],
          avoidLabelOverlap: true,
          itemStyle: {
            borderRadius: 6,
            borderColor: "#fff",
            borderWidth: 2,
          },
          label: {
            show: true,
            formatter: "{b}\n{d}%",
          },
          emphasis: {
            label: {
              show: true,
              fontSize: 14,
              fontWeight: "bold" as const,
            },
          },
          data: currentData.map((item) => ({
            name: item.name,
            value: item.revenue,
          })),
        },
      ],
    };
  }, [currentData, tab]);

  const barOption = useMemo(() => {
    const trend = business.revenueTrend5y ?? [];
    // 最后一个值可能是当年不完整数据，仍显示但标注
    const years: string[] = [];
    const currentYear = new Date().getFullYear();
    const n = trend.length;
    for (let i = n - 1; i >= 0; i--) {
      years.push(`${currentYear - i}`);
    }

    return {
      backgroundColor: "transparent",
      tooltip: {
        trigger: "axis" as const,
        formatter: (params: any) => {
          const p = params[0];
          return `${p.name}年<br/>营收: ${p.value?.toFixed(2)} 亿`;
        },
      },
      grid: { left: 60, right: 30, top: 30, bottom: 40 },
      xAxis: {
        type: "category" as const,
        data: years,
        axisLabel: { fontSize: 12 },
      },
      yAxis: {
        type: "value" as const,
        name: "营收(亿)",
        axisLabel: { fontSize: 12 },
      },
      series: [
        {
          name: "年度营收",
          type: "bar" as const,
          data: trend,
          itemStyle: {
            color: "#5470c6",
            borderRadius: [4, 4, 0, 0],
          },
          barWidth: "50%",
        },
      ],
    };
  }, [business.revenueTrend5y]);

  if (!hasAny) {
    return (
      <div className="rounded-lg border bg-card p-6">
        <h3 className="text-lg font-semibold mb-4">主营构成</h3>
        <div className="flex items-center justify-center min-h-[16rem] text-muted-foreground">
          <span>暂无主营业务构成数据</span>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-lg border bg-card p-4 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h3 className="text-lg font-semibold">主营构成</h3>
        {hasProduct && hasRegion && (
          <div className="inline-flex rounded-lg border overflow-hidden">
            <button
              className={`px-4 py-1.5 text-sm font-medium transition-colors ${
                tab === "product"
                  ? "bg-primary text-primary-foreground"
                  : "bg-background hover:bg-muted"
              }`}
              onClick={() => setTab("product")}
            >
              按产品
            </button>
            <button
              className={`px-4 py-1.5 text-sm font-medium transition-colors ${
                tab === "region"
                  ? "bg-primary text-primary-foreground"
                  : "bg-background hover:bg-muted"
              }`}
              onClick={() => setTab("region")}
            >
              按地区
            </button>
          </div>
        )}
      </div>

      <ReactECharts
        option={pieOption}
        style={{ height: "320px", width: "100%" }}
        notMerge
        lazyUpdate
      />

      {(business.revenueTrend5y ?? []).length > 0 && (
        <div>
          <h4 className="text-sm font-medium text-muted-foreground mb-2">
            近5年营收趋势
          </h4>
          <ReactECharts
            option={barOption}
            style={{ height: "240px", width: "100%" }}
            notMerge
            lazyUpdate
          />
        </div>
      )}
    </div>
  );
}
