// components/vol-surface/VolSurfaceTabs.tsx
// 标签页切换：3D 曲面 / 微笑曲线 / 期限结构

"use client";

import { useState } from "react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { VolSurface3D } from "./VolSurface3D";
import { VolSmileChart } from "./VolSmileChart";
import { TermStructureChart } from "./TermStructureChart";

interface VolSurfaceTabsProps {
  underlying: string;
  date?: string;
  selectedExpiry?: string;
  onExpiryChange?: (expiry: string) => void;
}

export function VolSurfaceTabs({
  underlying,
  date,
  selectedExpiry,
}: VolSurfaceTabsProps) {
  const [activeTab, setActiveTab] = useState<"3d" | "smile" | "term">("3d");
  const [refreshKey, setRefreshKey] = useState(0);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap justify-between items-center gap-2">
        <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as any)}>
          <TabsList>
            <TabsTrigger value="3d">3D 曲面</TabsTrigger>
            {/* 微笑曲线 Tab 不再 disabled，用户可随时切换 */}
            <TabsTrigger value="smile">微笑曲线</TabsTrigger>
            <TabsTrigger value="term">期限结构</TabsTrigger>
          </TabsList>

          <TabsContent value="3d" className="mt-4">
            <VolSurface3D
              underlying={underlying}
              date={date}
              refreshKey={refreshKey}
            />
          </TabsContent>

          <TabsContent value="smile" className="mt-4">
            {selectedExpiry ? (
              <VolSmileChart
                underlying={underlying}
                expiry={selectedExpiry}
                date={date}
                refreshKey={refreshKey}
              />
            ) : (
              <div className="rounded-lg border bg-muted/30 p-12 text-center text-muted-foreground">
                请先选择到期月
              </div>
            )}
          </TabsContent>

          <TabsContent value="term" className="mt-4">
            <TermStructureChart
              underlying={underlying}
              date={date}
              refreshKey={refreshKey}
            />
          </TabsContent>
        </Tabs>

        <button
          onClick={() => setRefreshKey((k) => k + 1)}
          className="text-sm text-muted-foreground hover:text-foreground border rounded-md px-3 py-1.5"
          title="刷新数据"
        >
          刷新
        </button>
      </div>
    </div>
  );
}
