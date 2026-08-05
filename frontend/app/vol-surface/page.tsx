// app/vol-surface/page.tsx
// 波动率分析页面（SSR + CSR 混合）
// useSearchParams() 需要 Suspense boundary，参考 options-chain/page.tsx 的做法

import { Suspense } from "react";
import { VolSurfaceClient } from "./vol-surface-client";

export const revalidate = 300; // 5 分钟缓存

export default function VolSurfacePage() {
  return (
    <Suspense
      fallback={
        <div className="container py-6">
          <h1 className="text-2xl font-semibold mb-4">波动率分析</h1>
          <p className="text-muted-foreground text-sm">加载中...</p>
        </div>
      }
    >
      <VolSurfaceClient />
    </Suspense>
  );
}
