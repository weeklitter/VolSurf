// components/vol-surface/VolSmileChart.tsx
// ECharts 波动率微笑曲线
// - Call / Put 双线
// - ATM IV 标记线（yAxis 水平虚线）
// - 25 Delta Skew 区域标注（OTM Put 区域淡红色背景）
// - 深色模式自适应
// - 底部统计栏

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import { formatIv } from "@/lib/utils";
import type { VolSmileResponse, SmilePoint } from "@/lib/types";
import * as echarts from "echarts/core";
import { LineChart, ScatterChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkLineComponent,
  MarkPointComponent,
  MarkAreaComponent,
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
  MarkAreaComponent,
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

  // ── 数据排序 ──
  const calls = [...data.calls].sort(
    (a: SmilePoint, b: SmilePoint) => a.strike - b.strike
  );
  const puts = [...data.puts].sort(
    (a: SmilePoint, b: SmilePoint) => a.strike - b.strike
  );

  // ATM 行权价：取 delta 绝对值最接近 0.5 的 put strike
  const atmPut = puts.reduce((closest: SmilePoint | null, p: SmilePoint) => {
    if (!closest) return p;
    return Math.abs(p.delta + 0.5) < Math.abs(closest.delta + 0.5)
      ? p
      : closest;
  }, null);
  const atmStrike = atmPut?.strike ?? calls[Math.floor(calls.length / 2)]?.strike ?? 0;
  const atmIv = data.atmIv;

  // ── 25Δ Skew 区域标注 ──
  // skew25 > 0 表示 OTM Put IV 偏高（Put 翼偏斜），用淡红色标注 OTM Put 区域
  const hasSkew = data.skew25 > 0;
  // OTM Put 区域：行权价 < ATM 的部分
  const otmPutStrikes = puts
    .filter((p: SmilePoint) => p.strike < atmStrike)
    .map((p: SmilePoint) => p.strike);
  const skewAreaStart = otmPutStrikes.length > 0 ? otmPutStrikes[0] : 0;
  const skewAreaEnd = atmStrike;

  // ── 所有 IV 值范围（用于 yAxis 缩放） ──
  const allIvs = [...calls.map((c) => c.iv), ...puts.map((p) => p.iv), atmIv];
  const ivMin = Math.min(...allIvs);
  const ivMax = Math.max(...allIvs);
  const ivRange = ivMax - ivMin || ivMax * 0.1;

  const option = {
    backgroundColor: "transparent",
    title: {
      text: `波动率微笑 - ${data.expiry}`,
      subtext: `ATM IV ${formatIv(atmIv)}  |  25Δ Skew ${formatIv(data.skew25, 2)}`,
      left: "center",
      textStyle: { fontSize: 16 },
    },
    tooltip: {
      trigger: "item" as const,
      formatter: (params: any) => {
        // data 格式: [strike, iv, delta]
        const d = params.data;
        // markLine 的 data 可能是对象格式，跳过
        if (!Array.isArray(d)) return "";
        const strike = d[0];
        const iv = d[1];
        const delta = d[2];
        const type = params.seriesName;
        return `<b>${type}</b><br/>行权价: ${strike.toFixed(4)}<br/>IV: ${formatIv(iv, 2)}<br/>Delta: ${delta.toFixed(3)}`;
      },
    },
    legend: {
      data: ["认购 IV", "认沽 IV"],
      top: 55,
    },
    grid: { left: 60, right: 30, top: 100, bottom: 60 },
    xAxis: {
      type: "value" as const,
      name: "行权价",
      nameLocation: "middle" as const,
      nameGap: 30,
      scale: true,
    },
    yAxis: {
      type: "value" as const,
      name: "IV",
      min: Math.max(0, ivMin - ivRange * 0.2).toFixed(4),
      max: (ivMax + ivRange * 0.2).toFixed(4),
      axisLabel: {
        formatter: (v: number) => `${(v * 100).toFixed(1)}%`,
      },
    },
    series: [
      {
        name: "认购 IV",
        type: "line" as const,
        data: calls.map((c: SmilePoint) => [c.strike, c.iv, c.delta]),
        smooth: true,
        symbol: "circle",
        symbolSize: 6,
        itemStyle: { color: "#d62728" },
        lineStyle: { width: 2 },
        // ATM IV 水平标记线
        markLine: {
          symbol: "none",
          silent: true,
          data: [
            {
              yAxis: atmIv,
              label: {
                formatter: `ATM ${formatIv(atmIv, 2)}`,
                position: "insideEndTop",
              },
              lineStyle: { color: "#999", type: "dashed" as const, width: 1.5 },
            },
            // ATM 行权价垂直线
            {
              xAxis: atmStrike,
              label: {
                formatter: `ATM K=${atmStrike.toFixed(4)}`,
                position: "start",
              },
              lineStyle: { color: "#999", type: "dashed" as const, width: 1 },
            },
          ],
        },
        // 25Δ Skew 区域标注
        markArea:
          hasSkew && skewAreaEnd > skewAreaStart
            ? {
                silent: true,
                itemStyle: {
                  color: "rgba(214, 39, 40, 0.08)",
                  borderColor: "rgba(214, 39, 40, 0.3)",
                  borderWidth: 1,
                  borderType: "dashed" as const,
                },
                label: {
                  show: true,
                  position: "top",
                  formatter: "OTM Put 翼\n25Δ Skew 偏高",
                  fontSize: 10,
                  color: "#d62728",
                  opacity: 0.8,
                },
                data: [
                  [
                    { xAxis: skewAreaStart },
                    { xAxis: skewAreaEnd },
                  ],
                ],
              }
            : undefined,
      },
      {
        name: "认沽 IV",
        type: "line" as const,
        data: puts.map((p: SmilePoint) => [p.strike, p.iv, p.delta]),
        smooth: true,
        symbol: "circle",
        symbolSize: 6,
        itemStyle: { color: "#1f77b4" },
        lineStyle: { width: 2 },
      },
    ],
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
            Call {calls.length} · Put {puts.length}
          </span>
        </div>
      </div>
    </div>
  );
}
