// components/stock/StockSearch.tsx
// 带防抖的股票搜索输入框（客户端组件）

"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import { Search, Loader2 } from "lucide-react";
import { api } from "@/lib/api";
import type { StockSearchResult } from "@/lib/types";
import { cn } from "@/lib/utils";

export function StockSearch({ className }: { className?: string }) {
  const router = useRouter();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<StockSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [highlightIndex, setHighlightIndex] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  // 防抖搜索
  useEffect(() => {
    if (query.trim().length < 1) {
      setResults([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    const timer = setTimeout(async () => {
      // 取消上一次请求
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      try {
        const data = await api.searchStocks(query.trim());
        if (!controller.signal.aborted) {
          setResults(data);
          setShowDropdown(true);
          setHighlightIndex(-1);
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          setResults([]);
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [query]);

  // 点击外部关闭下拉
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setShowDropdown(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = useCallback(
    (tsCode: string) => {
      setShowDropdown(false);
      setQuery("");
      setResults([]);
      router.push(`/stocks/${tsCode}`);
    },
    [router]
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!showDropdown || results.length === 0) return;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setHighlightIndex((prev) =>
        prev < results.length - 1 ? prev + 1 : 0
      );
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setHighlightIndex((prev) =>
        prev > 0 ? prev - 1 : results.length - 1
      );
    } else if (e.key === "Enter") {
      e.preventDefault();
      const idx = highlightIndex >= 0 ? highlightIndex : 0;
      if (results[idx]) {
        handleSelect(results[idx].tsCode);
      }
    } else if (e.key === "Escape") {
      setShowDropdown(false);
    }
  };

  return (
    <div ref={containerRef} className={cn("relative", className)}>
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          onFocus={() => results.length > 0 && setShowDropdown(true)}
          placeholder="搜索股票名称 / 代码..."
          className="w-full pl-9 pr-9 py-2 rounded-lg border bg-background text-sm transition-colors focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
        />
        {loading && (
          <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 animate-spin text-muted-foreground" />
        )}
      </div>

      {showDropdown && results.length > 0 && (
        <ul className="absolute z-50 mt-1 w-full rounded-lg border bg-popover shadow-lg max-h-80 overflow-y-auto">
          {results.map((item, idx) => (
            <li
              key={item.tsCode}
              onMouseDown={(e) => {
                e.preventDefault();
                handleSelect(item.tsCode);
              }}
              onMouseEnter={() => setHighlightIndex(idx)}
              className={cn(
                "flex items-center justify-between px-3 py-2 cursor-pointer text-sm transition-colors",
                idx === highlightIndex
                  ? "bg-accent text-accent-foreground"
                  : "hover:bg-accent/50"
              )}
            >
              <div className="flex items-center gap-2 min-w-0">
                <span className="font-medium truncate">{item.name}</span>
                <span className="text-xs text-muted-foreground shrink-0">
                  {item.tsCode}
                </span>
              </div>
              <div className="flex items-center gap-2 text-xs text-muted-foreground shrink-0 ml-2">
                {item.industry && (
                  <span className="px-1.5 py-0.5 rounded bg-muted">
                    {item.industry}
                  </span>
                )}
                {item.market && (
                  <span className="px-1.5 py-0.5 rounded bg-muted">
                    {item.market}
                  </span>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      {showDropdown && !loading && query.trim() && results.length === 0 && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border bg-popover shadow-lg px-3 py-4 text-center text-sm text-muted-foreground">
          未找到匹配的股票
        </div>
      )}
    </div>
  );
}
