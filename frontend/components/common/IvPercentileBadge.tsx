// components/common/IvPercentileBadge.tsx
// IV 百分位标签 - 根据百分位高低显示不同颜色

import { TrendingDown, TrendingUp, Minus } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatPercent, formatIv } from "@/lib/utils";

interface IvPercentileBadgeProps {
  percentile: number | null | undefined;
  atmIv?: number | null;
  className?: string;
}

export function IvPercentileBadge({
  percentile,
  atmIv,
  className,
}: IvPercentileBadgeProps) {
  if (percentile == null) {
    return (
      <span
        className={cn(
          "inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs",
          className
        )}
      >
        IV: —
      </span>
    );
  }

  // 颜色阈值: < 25 低位, 25-75 中位, > 75 高位
  let colorClass: string;
  let Icon: typeof TrendingUp;
  let levelText: string;
  if (percentile < 25) {
    colorClass = "bg-green-50 text-green-700 border-green-200";
    Icon = TrendingDown;
    levelText = "低";
  } else if (percentile > 75) {
    colorClass = "bg-red-50 text-red-700 border-red-200";
    Icon = TrendingUp;
    levelText = "高";
  } else {
    colorClass = "bg-yellow-50 text-yellow-700 border-yellow-200";
    Icon = Minus;
    levelText = "中";
  }

  return (
    <span
      title={`IV 百分位 ${formatPercent(percentile, 1)}（${levelText}位）${
        atmIv != null ? `  ATM IV ${formatIv(atmIv)}` : ""
      }`}
      className={cn(
        "inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs font-medium",
        colorClass,
        className
      )}
    >
      <Icon className="h-3 w-3" />
      <span>IV分位 {formatPercent(percentile, 1)}</span>
      {atmIv != null && <span className="opacity-70">({formatIv(atmIv)})</span>}
    </span>
  );
}
