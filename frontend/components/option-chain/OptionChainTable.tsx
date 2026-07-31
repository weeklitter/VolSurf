// components/option-chain/OptionChainTable.tsx
// TanStack Table T 型报价表
// 中间列：行权价（黄色背景）
// 左列：认沽 (Put)
// 右列：认购 (Call)

"use client";

import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { useMemo, useState } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ArrowUpDown } from "lucide-react";
import { cn, formatDelta, formatIv, formatPrice } from "@/lib/utils";

// ── T 型报价行结构（合并 Call/Put） ──
export interface OptionChainRow {
  strike: number;
  call: {
    tsCode: string;
    settle: number | null;
    volume: number;
    openInterest: number;
    iv: number | null;
    delta: number | null;
    ivConfidence: boolean;
  };
  put: {
    tsCode: string;
    settle: number | null;
    volume: number;
    openInterest: number;
    iv: number | null;
    delta: number | null;
    ivConfidence: boolean;
  };
}

interface OptionChainTableProps {
  data: {
    calls: Array<{
      tsCode: string;
      strike: number;
      settle: number | null;
      volume: number;
      openInterest: number;
      iv: number | null;
      delta: number | null;
      ivConfidence: boolean;
    }>;
    puts: Array<{
      tsCode: string;
      strike: number;
      settle: number | null;
      volume: number;
      openInterest: number;
      iv: number | null;
      delta: number | null;
      ivConfidence: boolean;
    }>;
    underlying: { tsCode: string; name: string; price: number };
  };
}

// 数字单元格：高亮显示
function NumCell({
  value,
  digits = 2,
  className,
  dimWhenZero = false,
}: {
  value: number | null | undefined;
  digits?: number;
  className?: string;
  dimWhenZero?: boolean;
}) {
  const isNull = value == null;
  const isZero = !isNull && dimWhenZero && value === 0;
  return (
    <span
      className={cn(
        "tabular-nums",
        isNull && "text-muted-foreground",
        isZero && "text-muted-foreground/50",
        className
      )}
    >
      {isNull ? "—" : digits === 0 ? value.toFixed(0) : value.toFixed(digits)}
    </span>
  );
}

export function OptionChainTable({ data }: OptionChainTableProps) {
  // ── 合并 Call/Put 为 T 型行（按 strike 对齐） ──
  const rows = useMemo<OptionChainRow[]>(() => {
    const map = new Map<number, OptionChainRow>();
    for (const c of data.calls) {
      map.set(c.strike, {
        strike: c.strike,
        call: {
          tsCode: c.tsCode,
          settle: c.settle,
          volume: c.volume,
          openInterest: c.openInterest,
          iv: c.iv,
          delta: c.delta,
          ivConfidence: c.ivConfidence,
        },
        put: {
          tsCode: "",
          settle: null,
          volume: 0,
          openInterest: 0,
          iv: null,
          delta: null,
          ivConfidence: true,
        },
      });
    }
    for (const p of data.puts) {
      const existing = map.get(p.strike);
      if (existing) {
        existing.put = {
          tsCode: p.tsCode,
          settle: p.settle,
          volume: p.volume,
          openInterest: p.openInterest,
          iv: p.iv,
          delta: p.delta,
          ivConfidence: p.ivConfidence,
        };
      } else {
        map.set(p.strike, {
          strike: p.strike,
          call: {
            tsCode: "",
            settle: null,
            volume: 0,
            openInterest: 0,
            iv: null,
            delta: null,
            ivConfidence: true,
          },
          put: {
            tsCode: p.tsCode,
            settle: p.settle,
            volume: p.volume,
            openInterest: p.openInterest,
            iv: p.iv,
            delta: p.delta,
            ivConfidence: p.ivConfidence,
          },
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.strike - b.strike);
  }, [data]);

  // ── 列定义（T 型：左 Put / 中 Strike / 右 Call） ──
  const columns = useMemo<ColumnDef<OptionChainRow>[]>(
    () => [
      // ── 认沽 (Put) 列：右对齐，颜色淡蓝 ──
      {
        id: "put_iv",
        header: () => <div className="text-right">IV</div>,
        cell: ({ row }) => (
          <div className="text-right">
            <NumCell value={row.original.put.iv} digits={4} />
          </div>
        ),
      },
      {
        id: "put_delta",
        header: () => <div className="text-right">Delta</div>,
        cell: ({ row }) => (
          <div className="text-right">
            <NumCell value={row.original.put.delta} digits={3} />
          </div>
        ),
      },
      {
        id: "put_oi",
        header: () => <div className="text-right">持仓量</div>,
        cell: ({ row }) => (
          <div className="text-right">
            <NumCell value={row.original.put.openInterest} digits={0} dimWhenZero />
          </div>
        ),
      },
      {
        id: "put_vol",
        header: () => <div className="text-right">成交量</div>,
        cell: ({ row }) => (
          <div className="text-right">
            <NumCell value={row.original.put.volume} digits={0} dimWhenZero />
          </div>
        ),
      },
      {
        id: "put_settle",
        header: () => <div className="text-right">结算价</div>,
        cell: ({ row }) => (
          <div className="text-right font-medium">
            <NumCell value={row.original.put.settle} digits={4} />
          </div>
        ),
      },
      // ── 中间：行权价（高亮列） ──
      {
        id: "strike",
        header: () => <div className="text-center">行权价</div>,
        cell: ({ row }) => {
          const s = data.underlying.price;
          const moneyness = s > 0 ? (s / row.original.strike - 1) * 100 : 0;
          const isAtm = Math.abs(moneyness) < 0.5;
          return (
            <div
              className={cn(
                "text-center font-semibold tabular-nums",
                isAtm && "text-primary"
              )}
            >
              {row.original.strike.toFixed(4)}
              {!isAtm && (
                <div className="text-[10px] text-muted-foreground font-normal">
                  {moneyness > 0 ? "-" : "+"}
                  {Math.abs(moneyness).toFixed(1)}%
                </div>
              )}
            </div>
          );
        },
      },
      // ── 认购 (Call) 列：左对齐，颜色淡红 ──
      {
        id: "call_settle",
        header: () => <div className="text-left">结算价</div>,
        cell: ({ row }) => (
          <div className="text-left font-medium">
            <NumCell value={row.original.call.settle} digits={4} />
          </div>
        ),
      },
      {
        id: "call_vol",
        header: () => <div className="text-left">成交量</div>,
        cell: ({ row }) => (
          <div className="text-left">
            <NumCell value={row.original.call.volume} digits={0} dimWhenZero />
          </div>
        ),
      },
      {
        id: "call_oi",
        header: () => <div className="text-left">持仓量</div>,
        cell: ({ row }) => (
          <div className="text-left">
            <NumCell value={row.original.call.openInterest} digits={0} dimWhenZero />
          </div>
        ),
      },
      {
        id: "call_delta",
        header: () => <div className="text-left">Delta</div>,
        cell: ({ row }) => (
          <div className="text-left">
            <NumCell value={row.original.call.delta} digits={3} />
          </div>
        ),
      },
      {
        id: "call_iv",
        header: () => <div className="text-left">IV</div>,
        cell: ({ row }) => (
          <div className="text-left">
            <NumCell value={row.original.call.iv} digits={4} />
          </div>
        ),
      },
    ],
    [data.underlying.price]
  );

  const [sorting, setSorting] = useState<SortingState>([]);

  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });

  return (
    <div className="rounded-lg border bg-card overflow-hidden">
      <div className="overflow-x-auto">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((hg) => (
              <TableRow key={hg.id} className="hover:bg-transparent">
                {hg.headers.map((header) => {
                  const isCenter = header.id === "strike";
                  const isRight = header.id.startsWith("put_");
                  return (
                    <TableHead
                      key={header.id}
                      className={cn(
                        "whitespace-nowrap text-xs",
                        isCenter && "bg-yellow-100/50 dark:bg-yellow-900/20",
                        isRight && "text-right",
                        isCenter && "text-center"
                      )}
                    >
                      {flexRender(header.column.columnDef.header, header.getContext())}
                    </TableHead>
                  );
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-32 text-center text-muted-foreground"
                >
                  暂无期权链数据
                </TableCell>
              </TableRow>
            ) : (
              table.getRowModel().rows.map((row) => {
                // ATM 行加底色
                const s = data.underlying.price;
                const moneyness = s > 0 ? s / row.original.strike : 0;
                const isAtm = Math.abs(moneyness - 1) < 0.005;
                return (
                  <TableRow
                    key={row.original.strike}
                    className={cn(isAtm && "bg-yellow-50/50 dark:bg-yellow-900/10")}
                  >
                    {row.getVisibleCells().map((cell) => {
                      const isCenter = cell.column.id === "strike";
                      const isRight = cell.column.id.startsWith("put_");
                      return (
                        <TableCell
                          key={cell.id}
                          className={cn(
                            "text-sm",
                            isCenter && "bg-yellow-50/50 dark:bg-yellow-900/20 font-semibold",
                            isRight && "text-right"
                          )}
                        >
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </TableCell>
                      );
                    })}
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
