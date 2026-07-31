// components/option-chain/ExpirySelector.tsx
// 到期月选择器

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

interface ExpirySelectorProps {
  underlying: string;
  date?: string;
}

export function ExpirySelector({ underlying, date }: ExpirySelectorProps) {
  const router = useRouter();
  const params = useSearchParams();
  const current = params.get("expiry") || "";

  const [expiries, setExpiries] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!underlying) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .getExpiries(underlying, date)
      .then((res) => {
        if (cancelled) return;
        setExpiries(res);
        // 自动选中第一个到期月（如果 URL 中没有）
        if (!current && res.length > 0) {
          const p = new URLSearchParams(params);
          p.set("expiry", res[0]);
          router.replace(`?${p.toString()}`);
        }
      })
      .catch((e: ApiError) => {
        if (!cancelled) setError(e.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [underlying, date]); // eslint-disable-line react-hooks/exhaustive-deps

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
        p.set("expiry", v);
        router.push(`?${p.toString()}`);
      }}
    >
      <SelectTrigger className="w-[180px]">
        <SelectValue placeholder="选择到期月" />
      </SelectTrigger>
      <SelectContent>
        {expiries.map((e) => (
          <SelectItem key={e} value={e}>
            {e}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
