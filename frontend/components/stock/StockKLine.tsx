// components/stock/StockKLine.tsx
// 区域5：K线图
// - 调用 api.getStockDaily(tsCode, start, end) 获取近1年K线数据
// - ECharts candlestick + 成交量 + MA20/60/120均线
// - dataZoom 支持缩放
// - 中国股市配色：涨红跌绿

"use client";

import { useEffect, useState } from "react";
import * as echarts from "echarts/core";
import { CandlestickChart, BarChart, LineChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  DataZoomComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import ReactECharts from "echarts-for-react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import type { StockDailyData } from "@/lib/types";

echarts.use([
  CandlestickChart,
  BarChart,
  LineChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  DataZoomComponent,
  CanvasRenderer,
]);

interface StockKLineProps {
  tsCode: string;
}

/** 计算 MA 均线 */
function calcMA(data: number[], period: number): (number | null)[] {
  const result: (number | null)[] = [];
  for (let i = 0; i < data.length; i++) {
    if (i < period - 1) {
      result.push(null);
    } else {
      const slice = data.slice(i - period + 1, i + 1);
      const sum = slice.reduce((a, b) => a + b, 0);
      result.push(sum / period);
    }
  }
  return result;
}

export function StockKLine({ tsCode }: StockKLineProps) {
  const [data, setData] = useState<StockDailyData[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    // 计算近1年日期范围
    const end = new Date();
    const start = new Date();
    start.setFullYear(start.getFullYear() - 1);
    const fmt = (d: Date) =>
      `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

    api
      .getStockDaily(tsCode, fmt(start), fmt(end))
      .then((res) => {
        if (!cancelled) setData(res ?? []);
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
  }, [tsCode]);

  if (loading) return <Loading message="加载K线数据..." />;
  if (error) return <ErrorState message={error} />;
  if (!data || data.length === 0)
    return (
      <div className="rounded-lg border bg-card p-6">
        <h3 className="text-lg font-semibold mb-4">K线走势</h3>
        <div className="flex items-center justify-center min-h-[16rem] text-muted-foreground">
          暂无K线数据
        </div>
      </div>
    );

  const dates = data.map((d) => d.date);
  // ECharts candlestick: [open, close, low, high]
  const ohlc = data.map((d) => [d.open, d.close, d.low, d.high]);
  const closes = data.map((d) => d.close);
  // 成交量：涨红跌绿
  const volumes = data.map((d, i) => ({
    value: d.volume,
    itemStyle: {
      color: i > 0 && d.close >= data[i - 1].close ? "#ef4444" : "#22c55e",
    },
  }));

  const ma20 = calcMA(closes, 20);
  const ma60 = calcMA(closes, 60);
  const ma120 = calcMA(closes, 120);

  const option = {
    backgroundColor: "transparent",
    tooltip: {
      trigger: "axis" as const,
      axisPointer: { type: "cross" as const },
    },
    legend: {
      data: ["日K", "MA20", "MA60", "MA120"],
      top: 0,
    },
    grid: [
      { left: 60, right: 30, top: 40, height: "55%" },
      { left: 60, right: 30, top: "72%", height: "20%" },
    ],
    xAxis: [
      {
        type: "category" as const,
        data: dates,
        boundaryGap: true,
        axisLine: { onZero: false },
        splitLine: { show: false },
        min: "dataMin" as const,
        max: "dataMax" as const,
      },
      {
        type: "category" as const,
        gridIndex: 1,
        data: dates,
        boundaryGap: true,
        axisLine: { onZero: false },
        splitLine: { show: false },
        min: "dataMin" as const,
        max: "dataMax" as const,
      },
    ],
    yAxis: [
      {
        scale: true,
        splitArea: { show: true },
      },
      {
        gridIndex: 1,
        splitNumber: 2,
        axisLabel: { show: true },
      },
    ],
    dataZoom: [
      {
        type: "inside" as const,
        xAxisIndex: [0, 1],
        start: 50,
        end: 100,
      },
      {
        show: true,
        type: "slider" as const,
        xAxisIndex: [0, 1],
        top: "94%",
        start: 50,
        end: 100,
      },
    ],
    series: [
      {
        name: "日K",
        type: "candlestick" as const,
        data: ohlc,
        xAxisIndex: 0,
        yAxisIndex: 0,
        // 涨红跌绿
        itemStyle: {
          color: "#ef4444", // 阳线
          color0: "#22c55e", // 阴线
          borderColor: "#ef4444",
          borderColor0: "#22c55e",
        },
      },
      {
        name: "MA20",
        type: "line" as const,
        data: ma20,
        xAxisIndex: 0,
        yAxisIndex: 0,
        smooth: true,
        symbol: "none",
        lineStyle: { width: 1, color: "#fac858" },
      },
      {
        name: "MA60",
        type: "line" as const,
        data: ma60,
        xAxisIndex: 0,
        yAxisIndex: 0,
        smooth: true,
        symbol: "none",
        lineStyle: { width: 1, color: "#91cc75" },
      },
      {
        name: "MA120",
        type: "line" as const,
        data: ma120,
        xAxisIndex: 0,
        yAxisIndex: 0,
        smooth: true,
        symbol: "none",
        lineStyle: { width: 1, color: "#ee6666" },
      },
      {
        name: "成交量",
        type: "bar" as const,
        data: volumes,
        xAxisIndex: 1,
        yAxisIndex: 1,
      },
    ],
  };

  return (
    <div className="rounded-lg border bg-card p-4">
      <h3 className="text-lg font-semibold mb-2">K线走势（近1年）</h3>
      <ReactECharts
        option={option}
        style={{ height: "480px", width: "100%" }}
        notMerge
        lazyUpdate
      />
    </div>
  );
}
