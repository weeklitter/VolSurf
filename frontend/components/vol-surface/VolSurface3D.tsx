// components/vol-surface/VolSurface3D.tsx
// Plotly 3D 波动率曲面
// - lazy load Plotly（~3MB）
// - 真正的 3D surface 曲面 + 散点叠加（保留 hover 细节）
// - 精简 modebar、中文提示
// - 外部"重置视角"按钮（React state 驱动）
// - 支持交互：旋转、缩放、hover

"use client";

import dynamic from "next/dynamic";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import type { VolSurfaceResponse, VolSurfacePoint } from "@/lib/types";

// react-plotly.js 默认导出是 component，类型定义在 @types/react-plotly.js
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

/** 默认相机视角（与 layout 中保持一致，用于重置） */
const DEFAULT_CAMERA = { eye: { x: 1.6, y: 1.6, z: 1.2 } };

/**
 * 把散点数据整理到规则网格，供 surface trace 使用。
 * - x = moneyness 网格（升序去重）
 * - y = timeToExpiry 网格（升序去重）
 * - z[i][j] = 该 (x_j, y_i) 上的 IV（无数据点时用 null，Plotly 会留白）
 *
 * 保留散点 trace 叠加在曲面上，让用户能 hover 看到每点的 K / callPut 详情。
 */
function buildSurfaceGrid(points: VolSurfacePoint[]) {
  const xs = Array.from(new Set(points.map((p) => p.moneyness))).sort(
    (a, b) => a - b
  );
  const ys = Array.from(new Set(points.map((p) => p.timeToExpiry))).sort(
    (a, b) => a - b
  );

  // (y, x) -> iv  最后一写入胜（理论上同一 grid cell 很少重复）
  const ivMap = new Map<string, number>();
  for (const p of points) {
    ivMap.set(`${p.timeToExpiry}|${p.moneyness}`, p.iv);
  }

  const z: (number | null)[][] = ys.map((y) =>
    xs.map((x) => ivMap.get(`${y}|${x}`) ?? null)
  );

  return { xs, ys, z };
}

export function VolSurface3D({
  underlying,
  date,
  refreshKey = 0,
}: VolSurface3DProps) {
  const [data, setData] = useState<VolSurfaceResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  /** 每次 +1 触发 Plot 组件重挂载（重置视角） */
  const [resetTrigger, setResetTrigger] = useState(0);

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

  // 重置视角：通过 key 变化触发 Plot 组件重挂载，layout.camera 自动恢复默认
  const handleResetView = useCallback(() => {
    setResetTrigger((n) => n + 1);
  }, []);

  if (loading) return <Loading message="加载波动率曲面..." />;
  if (error) return <ErrorState message={error} />;
  if (!data || data.points.length === 0) {
    return <ErrorState message="暂无波动率数据" />;
  }

  // ── 曲面网格 ──
  const { xs, ys, z } = buildSurfaceGrid(data.points);

  // ── 配色（与原 palette 对齐的近似色阶） ──
  const colorscale: Array<[number, string]> = [
    [0.0, "#1f77b4"],
    [0.25, "#2ca02c"],
    [0.5, "#ff7f0e"],
    [0.75, "#d62728"],
    [1.0, "#9467bd"],
  ];

  // ── Trace 1: surface 曲面 ──
  // 用 `as Data` 绕过 @types/plotly.js 对 surface contours 字段的不完整定义
  const surfaceTrace = {
    type: "surface",
    x: xs,
    y: ys,
    z,
    colorscale,
    showscale: true,
    colorbar: {
      title: { text: "IV", side: "right" },
      thickness: 12,
      len: 0.7,
    },
    hovertemplate:
      "Moneyness: %{x:.3f}<br>到期: %{y:.3f} 年<br>IV: %{z:.4f}<extra></extra>",
    contours: {
      z: {
        show: true,
        usecolormap: true,
        highlightcolor: "#ff7f0e",
        project: { z: true },
      },
    },
    lighting: {
      ambient: 0.7,
      diffuse: 0.8,
      specular: 0.2,
      roughness: 0.5,
    },
    name: "IV 曲面",
  };

  // ── Trace 2: 原始散点（hover 显示 K / callPut） ──
  const scatterTrace = {
    type: "scatter3d",
    mode: "markers",
    x: data.points.map((p: VolSurfacePoint) => p.moneyness),
    y: data.points.map((p: VolSurfacePoint) => p.timeToExpiry),
    z: data.points.map((p: VolSurfacePoint) => p.iv),
    text: data.points.map(
      (p: VolSurfacePoint) =>
        `${p.callPut === "C" ? "认购" : "认沽"} K=${p.strike.toFixed(4)} ` +
        `IV=${(p.iv * 100).toFixed(2)}%`
    ),
    hovertemplate: "%{text}<extra></extra>",
    name: "数据点",
    marker: {
      size: 3,
      color: "#ffffff",
      opacity: 0.6,
      line: { color: "#1f2937", width: 0.5 },
    },
  };

  const traces = [surfaceTrace, scatterTrace];

  const layout = {
    title: {
      text: `${data.underlying} 波动率曲面 (S=${data.underlyingPrice.toFixed(4)})`,
      font: { size: 14 },
    },
    scene: {
      xaxis: { title: { text: "Moneyness (S/K)" } },
      yaxis: { title: { text: "到期时间 (年)" } },
      zaxis: { title: { text: "IV" } },
      camera: DEFAULT_CAMERA,
    },
    margin: { l: 0, r: 0, t: 40, b: 0 },
    height: 600,
    legend: { orientation: "h" as const, y: -0.05 },
    paper_bgcolor: "transparent",
  };

  // ── modebar 配置 ──
  // 去掉 resetLastSave（download 后会冒出来的"恢复"按钮）+ tableRotation、lasso2d、select2d 等 2D 工具
  const config = {
    responsive: true,
    displaylogo: false,
    // 设置中文 locale（Plotly 自带 zh-CN 翻译，含 modebar title 提示）
    locale: "zh-CN",
    // 精简：保留 Download / Zoom / Pan / Orbit / Turntable / Reset
    modeBarButtonsToRemove: [
      "resetLastSave",
      "autoScale2d",
      "lasso2d",
      "select2d",
      "tableRotation",
      "hoverClosest3d",
    ],
    toImageButtonOptions: {
      format: "png" as const,
      filename: `${data.underlying}_vol_surface_${data.date}`,
      height: 1200,
      width: 1600,
      scale: 2,
    },
  };

  return (
    <div className="rounded-lg border bg-card p-2">
      <div className="flex justify-end px-2 pt-1">
        <button
          onClick={handleResetView}
          className="text-xs text-muted-foreground hover:text-foreground border rounded-md px-3 py-1 transition-colors"
          title="重置 3D 视角到默认位置"
        >
          🔄 重置视角
        </button>
      </div>
      <Plot
        // key 绑定 resetTrigger，确保 layout.camera 变化被 react-plotly 重新应用
        key={resetTrigger}
        data={traces as never}
        layout={layout as never}
        style={{ width: "100%", height: "600px" }}
        useResizeHandler
        config={config as never}
      />
      <p className="text-xs text-muted-foreground px-2 py-1">
        💡 提示：左键拖动旋转、滚轮缩放、悬停查看详细 IV；点击右上「重置视角」可恢复默认相机
      </p>
    </div>
  );
}
