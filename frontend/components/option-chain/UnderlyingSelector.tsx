// components/option-chain/UnderlyingSelector.tsx
// 标的选择器 - SSR-friendly 客户端组件

"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { api, ApiError } from "@/lib/api";
import { Loading } from "@/components/common/Loading";
import { ErrorState } from "@/components/common/ErrorState";
import type { Underlying } from "@/lib/types";

export function UnderlyingSelector() {
  const router = useRouter();
  const params = useSearchParams();
  const current = params.get("underlying") || "510050";

  const [underlyings, setUnderlyings] = useState<Underlying[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getUnderlyings()
      .then(setUnderlyings)
      .catch((e: ApiError) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="w-[180px]">
        <Loading className="min-h-0" message="" />
      </div>
    );
  }
  if (error) return <ErrorState message={error} className="min-h-0" />;

  return (
    <Select
      value={current}
      onValueChange={(v) => {
        const p = new URLSearchParams(params);
        p.set("underlying", v);
        // 切换标的时重置 expiry（不同标的的到期月不同）
        p.delete("expiry");
        router.push(`?${p.toString()}`);
      }}
    >
      <SelectTrigger className="w-[220px] max-w-[60vw]">
        <SelectValue placeholder="选择标的" />
      </SelectTrigger>
      <SelectContent>
        {underlyings.map((u) => (
          <SelectItem key={u.tsCode} value={u.tsCode}>
            {u.name} ({u.tsCode})
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
