// app/stocks/stocks-client.tsx
// 股票首页客户端组件：搜索 + 行业筛选 + 列表分页

"use client";

import { useState, useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Building2, ChevronLeft, ChevronRight } from "lucide-react";
import { StockSearch } from "@/components/stock/StockSearch";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import { api, ApiError } from "@/lib/api";
import type { StockListResponse } from "@/lib/types";
import { cn } from "@/lib/utils";

interface StocksClientProps {
  industries: string[];
  initialStocks: StockListResponse | null;
  initialError: string | null;
  initialIndustry?: string;
  initialPage: number;
}

// 热门股票快捷入口
const HOT_STOCKS = [
  { tsCode: "600519.SH", name: "贵州茅台" },
  { tsCode: "300750.SZ", name: "宁德时代" },
  { tsCode: "000001.SZ", name: "平安银行" },
  { tsCode: "601318.SH", name: "中国平安" },
  { tsCode: "000858.SZ", name: "五粮液" },
  { tsCode: "002594.SZ", name: "比亚迪" },
];

const PAGE_SIZE = 20;

export function StocksClient({
  industries,
  initialStocks,
  initialError,
  initialIndustry,
  initialPage,
}: StocksClientProps) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [selectedIndustry, setSelectedIndustry] = useState<string | undefined>(
    initialIndustry
  );
  const [currentPage, setCurrentPage] = useState(initialPage);

  // 股票列表查询
  const { data, error, isLoading, refetch } = useQuery({
    queryKey: ["stock-list", selectedIndustry, currentPage],
    queryFn: () => api.getStockList(selectedIndustry, currentPage, PAGE_SIZE),
    initialData: initialStocks ?? undefined,
  });

  // 行业筛选
  const handleIndustryChange = useCallback(
    (industry: string | undefined) => {
      setSelectedIndustry(industry);
      setCurrentPage(1);
      const params = new URLSearchParams();
      if (industry) params.set("industry", industry);
      params.set("page", "1");
      router.push(`/stocks?${params.toString()}`);
    },
    [router]
  );

  // 翻页
  const handlePageChange = useCallback(
    (newPage: number) => {
      setCurrentPage(newPage);
      const params = new URLSearchParams(searchParams.toString());
      params.set("page", String(newPage));
      router.push(`/stocks?${params.toString()}`);
    },
    [router, searchParams]
  );

  const totalPages = data ? Math.ceil(data.total / PAGE_SIZE) : 0;
  const stocks = data?.stocks ?? [];

  return (
    <div className="space-y-4">
      {/* 搜索框 + 热门股票 */}
      <div className="flex flex-col gap-3">
        <StockSearch className="max-w-md" />
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-muted-foreground">热门：</span>
          {HOT_STOCKS.map((stock) => (
            <Link
              key={stock.tsCode}
              href={`/stocks/${stock.tsCode}`}
              className="text-xs px-2.5 py-1 rounded-full border bg-card hover:bg-accent hover:border-accent transition-colors"
            >
              {stock.name}
            </Link>
          ))}
        </div>
      </div>

      <div className="flex gap-4">
        {/* 左侧：行业筛选 */}
        <aside className="w-48 shrink-0 hidden md:block">
          <div className="rounded-lg border bg-card p-3 sticky top-20">
            <h3 className="text-sm font-semibold mb-2 flex items-center gap-1.5">
              <Building2 className="h-4 w-4" />
              行业筛选
            </h3>
            <ul className="space-y-0.5 max-h-[60vh] overflow-y-auto">
              <li>
                <button
                  onClick={() => handleIndustryChange(undefined)}
                  className={cn(
                    "w-full text-left text-sm px-2 py-1 rounded transition-colors",
                    !selectedIndustry
                      ? "bg-primary text-primary-foreground font-medium"
                      : "text-muted-foreground hover:bg-accent"
                  )}
                >
                  全部行业
                </button>
              </li>
              {industries.map((ind) => (
                <li key={ind}>
                  <button
                    onClick={() => handleIndustryChange(ind)}
                    className={cn(
                      "w-full text-left text-sm px-2 py-1 rounded transition-colors truncate",
                      selectedIndustry === ind
                        ? "bg-primary text-primary-foreground font-medium"
                        : "text-muted-foreground hover:bg-accent"
                    )}
                  >
                    {ind}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        </aside>

        {/* 主区域：股票列表 */}
        <div className="flex-1 min-w-0">
          {initialError && !data ? (
            <ErrorState
              message={initialError}
              onRetry={() => refetch()}
            />
          ) : isLoading ? (
            <Loading message="加载股票列表..." />
          ) : error ? (
            <ErrorState
              message={(error as ApiError).message || "加载失败"}
              onRetry={() => refetch()}
            />
          ) : stocks.length === 0 ? (
            <div className="rounded-lg border bg-muted/30 p-12 text-center text-muted-foreground">
              {selectedIndustry
                ? `「${selectedIndustry}」行业暂无股票数据`
                : "暂无股票数据"}
            </div>
          ) : (
            <>
              {/* 移动端行业筛选下拉 */}
              <div className="md:hidden mb-3">
                <select
                  value={selectedIndustry || ""}
                  onChange={(e) =>
                    handleIndustryChange(e.target.value || undefined)
                  }
                  className="w-full px-3 py-2 rounded-lg border bg-background text-sm"
                >
                  <option value="">全部行业</option>
                  {industries.map((ind) => (
                    <option key={ind} value={ind}>
                      {ind}
                    </option>
                  ))}
                </select>
              </div>

              <div className="rounded-lg border bg-card overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b bg-muted/50">
                      <th className="text-left px-4 py-2.5 font-medium text-muted-foreground">
                        代码
                      </th>
                      <th className="text-left px-4 py-2.5 font-medium text-muted-foreground">
                        名称
                      </th>
                      <th className="text-left px-4 py-2.5 font-medium text-muted-foreground hidden sm:table-cell">
                        行业
                      </th>
                      <th className="text-left px-4 py-2.5 font-medium text-muted-foreground hidden sm:table-cell">
                        市场
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {stocks.map((stock) => (
                      <tr
                        key={stock.tsCode}
                        className="border-b last:border-0 hover:bg-accent/50 transition-colors"
                      >
                        <td className="px-4 py-2.5">
                          <Link
                            href={`/stocks/${stock.tsCode}`}
                            className="font-mono text-xs text-primary hover:underline"
                          >
                            {stock.tsCode}
                          </Link>
                        </td>
                        <td className="px-4 py-2.5">
                          <Link
                            href={`/stocks/${stock.tsCode}`}
                            className="font-medium hover:text-primary transition-colors"
                          >
                            {stock.name}
                          </Link>
                        </td>
                        <td className="px-4 py-2.5 hidden sm:table-cell text-muted-foreground">
                          {stock.industry || "-"}
                        </td>
                        <td className="px-4 py-2.5 hidden sm:table-cell">
                          {stock.market && (
                            <span className="text-xs px-1.5 py-0.5 rounded bg-muted text-muted-foreground">
                              {stock.market}
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* 分页 */}
              {totalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <span className="text-xs text-muted-foreground">
                    共 {data?.total || 0} 只 · 第 {currentPage}/{totalPages} 页
                  </span>
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handlePageChange(currentPage - 1)}
                      disabled={currentPage <= 1}
                      className="p-1.5 rounded border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-accent transition-colors"
                    >
                      <ChevronLeft className="h-4 w-4" />
                    </button>
                    <button
                      onClick={() => handlePageChange(currentPage + 1)}
                      disabled={currentPage >= totalPages}
                      className="p-1.5 rounded border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-accent transition-colors"
                    >
                      <ChevronRight className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
