// components/stock/ScoreBadge.tsx
// 评分圆环 / 等级标签

import { cn } from "@/lib/utils";

type Level = "excellent" | "good" | "normal" | "warn" | "danger";

const LEVEL_STYLES: Record<Level, string> = {
  excellent: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-400",
  good: "bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-400",
  normal: "bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-400",
  warn: "bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-400",
  danger: "bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-400",
};

const LEVEL_LABELS: Record<Level, string> = {
  excellent: "优秀",
  good: "良好",
  normal: "正常",
  warn: "警示",
  danger: "危险",
};

export function LevelBadge({
  level,
  className,
}: {
  level: Level;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium",
        LEVEL_STYLES[level],
        className
      )}
    >
      {LEVEL_LABELS[level]}
    </span>
  );
}

export function ScoreRing({
  score,
  label,
  size = 80,
}: {
  score: number;
  label?: string;
  size?: number;
}) {
  const radius = (size - 8) / 2;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (score / 100) * circumference;
  const color =
    score >= 80
      ? "#10b981"
      : score >= 60
      ? "#3b82f6"
      : score >= 40
      ? "#f59e0b"
      : "#ef4444";

  return (
    <div
      className="flex flex-col items-center gap-1"
      style={{ width: size }}
    >
      <div className="relative" style={{ width: size, height: size }}>
        <svg width={size} height={size} className="-rotate-90">
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke="currentColor"
            strokeWidth={4}
            className="text-muted/30"
          />
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke={color}
            strokeWidth={4}
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            strokeLinecap="round"
          />
        </svg>
        <span
          className="absolute inset-0 flex items-center justify-center text-sm font-bold tabular-nums"
          style={{ color }}
        >
          {score.toFixed(1)}
        </span>
      </div>
      {label && (
        <span className="text-xs text-muted-foreground">{label}</span>
      )}
    </div>
  );
}
