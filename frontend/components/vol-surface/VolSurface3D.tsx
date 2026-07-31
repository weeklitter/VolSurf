// components/vol-surface/VolSurface3D.tsx
// Plotly 3D 波动率曲面
// - lazy load Plotly（~3MB）
// - 每个到期月一条 trace（marker scatter3d）
// - 支持交互：旋转、缩放、hover

"use client";

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";

const Plot = dynamic(() => import("react-plotly.js"), {
  ssr: false,
  loading: () => <Loading message="加载 3D 引擎中..." />,
});

interface VolSurface3DProps {
  underlying: string;
  date?: string;
  /** 刷新触发器（变化时重新拉取） */
  refreshKey?: number;
}

export function VolSurface3D({ underlying, date, refreshKey = 0 }: VolSurface3DProps) {
  const [data, setData] = useState<VolSurfaceResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .getVolSurface(underlying, date)
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

  if (loading) return <Loading message="加载波动率曲面..." />;
  if (error) return <ErrorState message={error} />;
  if (!data || data.points.length === 0) {
    return <ErrorState message="暂无波动率数据" />;
  }

  // 按到期月分组，每组一条 trace
  const expiries = Array.from(new Set(data.points.map((p) => p.expiry))).sort();

  // 配色方案
  const palette = [
    "#1f77b4",
    "#ff7f0e",
    "#2ca02c",
    "#d62728",
    "#9467bd",
    "#8c564b",
    "#e377c2",
    "#7f7f7f",
  ];

  const traces = expiries.map((exp, i) => {
    const expPoints = data.points.filter((p) => p.expiry === exp);
    return {
      type: "scatter3d" as const,
      mode: "markers" as const,
      x: expPoints.map((p) => p.moneyness),
      y: expPoints.map((p) => p.timeToExpiry),
      z: expPoints.map((p) => p.iv),
      text: expPoints.map(
        (p) =>
          `${p.callPut === "C" ? "认购" : "认沽"} K=${p.strike.toFixed(4)} ` +
          `IV=${(p.iv * 100).toFixed(2)}%`
      ),
      name: exp,
      marker: {
        size: 4,
        color: palette[i % palette.length],
        opacity: 0.8,
      },
    };
  });

  const layout = {
    title: {
      text: `${data.underlying} 波动率曲面 (S=${data.underlyingPrice.toFixed(4)})`,
      font: { size: 14 },
    },
    scene: {
      xaxis: { title: { text: "Moneyness (S/K)" } },
      yaxis: { title: { text: "到期时间 (年)" } },
      zaxis: { title: { text: "IV" } },
      camera: { eye: { x: 1.6, y: 1.6, z: 1.2 } },
    },
    margin: { l: 0, r: 0, t: 40, b: 0 },
    height: 600,
    legend: { orientation: "h" as const, y: -0.05 },
    paper_bgcolor: "transparent",
  };

  return (
    <div className="rounded-lg border bg-card p-2">
      <Plot
        data={traces}
        layout={layout}
        style={{ width: "100%", height: "600px" }}
        useResizeHandler
        config={{ responsive: true, displaylogo: false }}
      />
      <p className="text-xs text-muted-foreground px-2 py-1">
        💡 提示：可拖动旋转、滚轮缩放、悬停查看每个点的详细信息
      </p>
    </div>
  );
}
