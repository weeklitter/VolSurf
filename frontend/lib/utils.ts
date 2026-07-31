// lib/utils.ts
// shadcn/ui 工具函数
import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** 格式化 IV（百分比） */
export function formatIv(iv: number | null | undefined, digits = 2): string {
  if (iv == null) return "—";
  return `${(iv * 100).toFixed(digits)}%`;
}

/** 格式化价格 */
export function formatPrice(price: number | null | undefined, digits = 4): string {
  if (price == null) return "—";
  return price.toFixed(digits);
}

/** 格式化百分比 */
export function formatPercent(value: number | null | undefined, digits = 2): string {
  if (value == null) return "—";
  return `${value.toFixed(digits)}%`;
}

/** 格式化 Delta（带正负号） */
export function formatDelta(delta: number | null | undefined, digits = 3): string {
  if (delta == null) return "—";
  return delta.toFixed(digits);
}
