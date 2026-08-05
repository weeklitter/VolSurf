// components/vol-surface/TermStructureChart.tsx
// ECharts 期限结构（ATM IV vs 到期时间）
// - Contango / Backwardation / 水平 判断
// - 近远月利差标注（max IV - min IV 差值和百分比）
// - 深色模式自适应
// - 底部统计栏

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import { formatIv } from "@/lib/utils";
import type { TermStructureResponse } from "@/lib/types";
import * as echarts from "echarts/core";
import { LineChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkPointComponent,
  MarkLineComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";

echarts.use([
  LineChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  MarkPointComponent,
  MarkLineComponent,
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
  const minIv = Math.min(...sorted.map((p) => p.atmIv));
  const maxIv = Math.max(...sorted.map((p) => p.atmIv));

  // ── 期限结构类型判断 ──
  const first = sorted[0].atmIv; // 最近到期月 IV
  const last = sorted[sorted.length - 1].atmIv; // 最远到期月 IV
  const ivSpread = maxIv - minIv; // 绝对利差
  const ivSpreadPct = minIv > 0 ? (ivSpread / minIv) * 100 : 0; // 相对利差百分比

  // 判断逻辑：远月 IV 与近月 IV 差异超过 5% 才算 Contango/Backwardation
  let structureType = "水平";
  let structureColor = "text-yellow-600";
  if (last > first * 1.05) {
    structureType = "Contango（近低远高）";
    structureColor = "text-green-600";
  } else if (last < first * 0.95) {
    structureType = "Backwardation（近高远低）";
    structureColor = "text-red-600";
  }

  const option = {
    backgroundColor: "transparent",
    title: {
      text: "ATM 隐含波动率期限结构",
      subtext: `${data.underlying} · ${data.date}  |  ${structureType}  |  利差 ${formatIv(ivSpread, 2)} (${ivSpreadPct.toFixed(1)}%)`,
      left: "center",
      textStyle: { fontSize: 16 },
    },
    tooltip: {
      trigger: "axis" as const,
      formatter: (params: any) => {
        const p = params[0];
        const point = sorted[p.dataIndex];
        return `<b>${point.expiry}</b><br/>` +
          `${p.marker} ATM IV: ${formatIv(point.atmIv, 2)}<br/>` +
          `距到期: ${point.daysToExpiry} 天`;
      },
    },
    legend: { data: ["ATM IV"], top: 55 },
    grid: { left: 60, right: 30, top: 110, bottom: 60 },
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
        data: sorted.map((p, i) => ({
          value: p.atmIv,
          daysToExpiry: p.daysToExpiry,
          iv: p.atmIv,
          expiry: p.expiry,
        })),
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
          label: {
            formatter: (params: any) => formatIv(params.value, 1),
          },
        },
        // 连接近远月的趋势线
        markLine: {
          symbol: "none",
          silent: true,
          data: [
            {
              // 水平线标注均值
              yAxis: sorted.reduce((sum, p) => sum + p.atmIv, 0) / sorted.length,
              label: {
                formatter: "均值",
                position: "insideEndTop",
              },
              lineStyle: { color: "#999", type: "dotted" as const, width: 1 },
            },
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
          <span className={`font-medium ${structureColor}`}>{structureType}</span>
        </div>
        <div>
          <span className="text-muted-foreground">近远月利差：</span>
          <span className="font-medium">
            {formatIv(ivSpread, 2)}
            <span className="text-muted-foreground ml-1">
              ({ivSpreadPct.toFixed(1)}%)
            </span>
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">最近/最远：</span>
          <span className="font-medium">
            {sorted[0].expiry} → {sorted[sorted.length - 1].expiry}
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
