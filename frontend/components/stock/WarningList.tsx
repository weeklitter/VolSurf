// components/stock/WarningList.tsx
// 区域6：预警列表
// - 按 level 分组显示（danger 置顶，然后 warn，最后 info）
// - 每条预警：图标 + 消息 + 数值/阈值
// - 无预警时显示"✅ 暂未发现异常"
// 纯展示组件，不需要 "use client"

import { AlertTriangle, AlertCircle, Info, CheckCircle2 } from "lucide-react";
import type { Warning } from "@/lib/types";

interface WarningListProps {
  warnings: Warning[];
}

const LEVEL_ORDER: Record<string, number> = {
  danger: 0,
  warn: 1,
  info: 2,
};

const LEVEL_CONFIG: Record<
  string,
  { icon: typeof AlertTriangle; color: string; bgColor: string; label: string }
> = {
  danger: {
    icon: AlertTriangle,
    color: "text-red-600",
    bgColor: "bg-red-50 dark:bg-red-950/30 border-red-200 dark:border-red-800",
    label: "严重预警",
  },
  warn: {
    icon: AlertCircle,
    color: "text-orange-600",
    bgColor:
      "bg-orange-50 dark:bg-orange-950/30 border-orange-200 dark:border-orange-800",
    label: "风险提示",
  },
  info: {
    icon: Info,
    color: "text-blue-600",
    bgColor: "bg-blue-50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-800",
    label: "信息提示",
  },
};

export function WarningList({ warnings }: WarningListProps) {
  if (!warnings || warnings.length === 0) {
    return (
      <div className="rounded-lg border bg-card p-6">
        <h3 className="text-lg font-semibold mb-4">风险预警</h3>
        <div className="flex items-center justify-center min-h-[8rem] text-green-600">
          <CheckCircle2 className="h-6 w-6 mr-2" />
          <span className="text-sm font-medium">暂未发现异常</span>
        </div>
      </div>
    );
  }

  // 按 level 排序
  const sorted = [...warnings].sort((a, b) => {
    const orderA = LEVEL_ORDER[a.level] ?? 3;
    const orderB = LEVEL_ORDER[b.level] ?? 3;
    return orderA - orderB;
  });

  return (
    <div className="rounded-lg border bg-card p-6">
      <h3 className="text-lg font-semibold mb-4">
        风险预警
        <span className="ml-2 text-sm font-normal text-muted-foreground">
          （{warnings.length} 条）
        </span>
      </h3>
      <div className="space-y-3">
        {sorted.map((w, i) => {
          const config = LEVEL_CONFIG[w.level] ?? LEVEL_CONFIG.info;
          const Icon = config.icon;
          return (
            <div
              key={i}
              className={`flex items-start gap-3 rounded-lg border p-3 ${config.bgColor}`}
            >
              <Icon className={`h-5 w-5 flex-shrink-0 mt-0.5 ${config.color}`} />
              <div className="flex-1 min-w-0">
                <p className={`text-sm font-medium ${config.color}`}>
                  {w.message}
                </p>
                {(w.value != null || w.threshold != null) && (
                  <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
                    {w.value != null && (
                      <span>
                        当前值：<span className="font-medium">{w.value}</span>
                      </span>
                    )}
                    {w.threshold != null && (
                      <span>
                        预警阈值：<span className="font-medium">{w.threshold}</span>
                      </span>
                    )}
                    <span className="text-muted-foreground/60">[{w.type}]</span>
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
