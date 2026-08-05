// components/vol-surface/TermStructureChart.tsx
// ECharts 期限结构（ATM IV vs 到期时间）

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import { formatIv } from "@/lib/utils";
import type { TermStructureResponse } from "@/lib/types";
import * as echarts from "echarts/core";
import { LineChart, BarChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkPointComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";

echarts.use([
  LineChart,
  BarChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkPointComponent,
  CanvasRenderer,
]);

interface TermStructureChartProps {
  underlying: string;
  date?: string;
  refreshKey?: number;
}

export function TermStructureChart({
  underlying,
  date,
  refreshKey = 0,
}: TermStructureChartProps) {
  const [data, setData] = useState<TermStructureResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .getTermStructure(underlying, date)
      .then((res) => {
        if (!cancelled) setData(res);
      })
      .catch((err: ApiError) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [underlying, date, refreshKey]);

  if (loading) return <Loading message="加载期限结构..." />;
  if (error) return <ErrorState message={error} />;
  if (!data || data.points.length === 0) {
    return <ErrorState message="暂无期限结构数据" />;
  }

  // 升序：近到期 -> 远到期
  const sorted = [...data.points].sort((a, b) => a.daysToExpiry - b.daysToExpiry);
  const maxDays = Math.max(...sorted.map((p) => p.daysToExpiry));
  const minIv = Math.min(...sorted.map((p) => p.atmIv));
  const maxIv = Math.max(...sorted.map((p) => p.atmIv));

  const option = {
    title: {
      text: "ATM 隐含波动率期限结构",
      subtext: `${data.underlying} · ${data.date}`,
      left: "center",
    },
    tooltip: {
      trigger: "axis" as const,
      formatter: (params: any) => {
        const p = params[0];
        return `${p.axisValue}<br/>` +
          `${p.marker} ATM IV: ${formatIv(p.data.iv, 2)}<br/>` +
          `距到期: ${p.data.daysToExpiry} 天`;
      },
    },
    legend: { data: ["ATM IV"], top: 50 },
    grid: { left: 60, right: 30, top: 100, bottom: 60 },
    xAxis: {
      type: "category" as const,
      data: sorted.map((p) => p.expiry),
      name: "到期日",
      nameLocation: "middle" as const,
      nameGap: 30,
      axisLabel: { rotate: sorted.length > 6 ? 30 : 0 },
    },
    yAxis: {
      type: "value" as const,
      name: "ATM IV",
      min: Math.max(0, minIv * 0.95).toFixed(4),
      max: (maxIv * 1.05).toFixed(4),
      axisLabel: { formatter: (v: number) => `${(v * 100).toFixed(1)}%` },
    },
    series: [
      {
        name: "ATM IV",
        type: "line" as const,
        data: sorted.map((p) => ({ value: p.atmIv, daysToExpiry: p.daysToExpiry, iv: p.atmIv })),
        smooth: true,
        itemStyle: { color: "#1f77b4" },
        lineStyle: { width: 2 },
        symbol: "circle",
        symbolSize: 8,
        markPoint: {
          data: [
            { type: "max" as const, name: "最高" },
            { type: "min" as const, name: "最低" },
          ],
        },
        areaStyle: {
          color: {
            type: "linear" as const,
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: "rgba(31,119,180,0.4)" },
              { offset: 1, color: "rgba(31,119,180,0.05)" },
            ],
          },
        },
      },
    ],
  };

  // 判断期限结构类型
  const first = sorted[0].atmIv;
  const last = sorted[sorted.length - 1].atmIv;
  let structureType = "水平";
  if (last > first * 1.05) structureType = "上行（Contango）";
  else if (last < first * 0.95) structureType = "下行（Backwardation）";

  return (
    <div className="rounded-lg border bg-card p-2">
      <ReactECharts
        option={option}
        style={{ height: "500px", width: "100%" }}
        notMerge
        lazyUpdate
      />
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm px-2 py-3 border-t mt-2">
        <div>
          <span className="text-muted-foreground">期限结构：</span>
          <span className="font-medium">{structureType}</span>
        </div>
        <div>
          <span className="text-muted-foreground">最近到期：</span>
          <span className="font-medium">
            {sorted[0].expiry} ({sorted[0].daysToExpiry}天)
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">最远到期：</span>
          <span className="font-medium">
            {sorted[sorted.length - 1].expiry} ({sorted[sorted.length - 1].daysToExpiry}天)
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">到期月数：</span>
          <span className="font-medium">{sorted.length}</span>
        </div>
      </div>
    </div>
  );
}
