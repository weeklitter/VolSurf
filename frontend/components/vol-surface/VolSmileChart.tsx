// components/vol-surface/VolSmileChart.tsx
// ECharts 波动率微笑曲线
// - Call / Put 双线
// - ATM IV 标记
// - 25 Delta Skew 标记

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import { formatIv, formatPercent } from "@/lib/utils";
import * as echarts from "echarts/core";
import { LineChart, ScatterChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkLineComponent,
  MarkPointComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";

echarts.use([
  LineChart,
  ScatterChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkLineComponent,
  MarkPointComponent,
  CanvasRenderer,
]);

interface VolSmileChartProps {
  underlying: string;
  expiry: string;
  date?: string;
  refreshKey?: number;
}

export function VolSmileChart({
  underlying,
  expiry,
  date,
  refreshKey = 0,
}: VolSmileChartProps) {
  const [data, setData] = useState<VolSmileResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .getVolSmile(underlying, expiry, date)
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
  }, [underlying, expiry, date, refreshKey]);

  if (loading) return <Loading message="加载微笑曲线..." />;
  if (error) return <ErrorState message={error} />;
  if (!data) return <ErrorState message="暂无微笑曲线数据" />;

  // ATM IV 标记线
  const atmIv = data.atmIv;
  const allStrikes = Array.from(
    new Set([
      ...data.calls.map((c) => c.strike),
      ...data.puts.map((p) => p.strike),
    ])
  ).sort((a, b) => a - b);

  const option = {
    title: {
      text: `波动率微笑 - ${data.expiry}`,
      subtext: `ATM IV ${formatIv(atmIv)}  |  25Δ Skew ${formatIv(data.skew25, 2)}`,
      left: "center",
    },
    tooltip: {
      trigger: "item" as const,
      formatter: (params: any) => {
        const point = params.data;
        return `${point[0] ? "" : ""}行权价: ${(point[0] ?? point.strike).toFixed(4)}<br/>` +
          `IV: ${formatIv(point[1] ?? point.iv, 2)}<br/>` +
          `Delta: ${(point[2] ?? point.delta).toFixed(3)}`;
      },
    },
    legend: {
      data: ["认购 IV", "认沽 IV", "ATM IV"],
      top: 50,
    },
    grid: { left: 60, right: 30, top: 100, bottom: 60 },
    xAxis: {
      type: "value" as const,
      name: "行权价",
      nameLocation: "middle" as const,
      nameGap: 30,
    },
    yAxis: {
      type: "value" as const,
      name: "IV",
      axisLabel: {
        formatter: (v: number) => `${(v * 100).toFixed(1)}%`,
      },
    },
    series: [
      {
        name: "认购 IV",
        type: "line" as const,
        data: data.calls
          .sort((a, b) => a.strike - b.strike)
          .map((c) => [c.strike, c.iv, c.delta]),
        smooth: true,
        itemStyle: { color: "#d62728" },
        lineStyle: { width: 2 },
        markLine: {
          symbol: "none",
          data: [{ yAxis: atmIv, label: { formatter: `ATM ${formatIv(atmIv, 2)}` } }],
          lineStyle: { color: "#666", type: "dashed" as const },
        },
      },
      {
        name: "认沽 IV",
        type: "line" as const,
        data: data.puts
          .sort((a, b) => a.strike - b.strike)
          .map((p) => [p.strike, p.iv, p.delta]),
        smooth: true,
        itemStyle: { color: "#1f77b4" },
        lineStyle: { width: 2 },
      },
    ],
    // 暴露 ATM 行权价（中位数）作为辅助信息
  };

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
          <span className="text-muted-foreground">到期日：</span>
          <span className="font-medium">{data.expiry}</span>
        </div>
        <div>
          <span className="text-muted-foreground">ATM IV：</span>
          <span className="font-medium">{formatIv(data.atmIv)}</span>
        </div>
        <div>
          <span className="text-muted-foreground">25Δ Skew：</span>
          <span
            className={
              data.skew25 > 0
                ? "font-medium text-red-600"
                : "font-medium text-green-600"
            }
          >
            {formatIv(data.skew25, 2)}
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">数据点：</span>
          <span className="font-medium">
            {allStrikes.length} 行权价
          </span>
        </div>
      </div>
    </div>
  );
}
