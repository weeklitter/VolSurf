// app/page.tsx
// 首页：品种选择入口

import Link from "next/link";
import { ChevronRight, TrendingUp, BarChart3, LineChart, Globe } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { Underlying } from "@/lib/types";

export const revalidate = 300; // 5 分钟缓存

export default async function HomePage() {
  // SSR：服务端预取标的列表
  const underlyings = await api.getUnderlyings().catch(() => []);

  return (
    <div className="container py-12">
      {/* Hero */}
      <section className="text-center max-w-3xl mx-auto mb-16">
        <h1 className="text-4xl font-bold tracking-tight mb-4">
          VolSurf
        </h1>
        <p className="text-lg text-muted-foreground mb-2">
          ETF 期权 / 股指期权的隐含波动率分析平台
        </p>
        <p className="text-sm text-muted-foreground">
          数据来源：Tushare / AKShare · 基于 B-S 模型 · 仅供学习研究
        </p>
      </section>

      {/* 标的卡片 */}
      <section className="mb-16">
        <h2 className="text-2xl font-semibold mb-6 text-center">选择品种</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 max-w-5xl mx-auto">
          {underlyings.map((u) => (
            <UnderlyingCard key={u.tsCode} underlying={u} />
          ))}
        </div>
      </section>

      {/* 功能介绍 */}
      <section className="max-w-5xl mx-auto">
        <h2 className="text-2xl font-semibold mb-6 text-center">功能特性</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <FeatureCard
            icon={<LineChart className="h-6 w-6" />}
            title="期权链"
            description="T 型报价表，认购/认沽并排展示，含 IV、Delta、成交量、持仓量等关键指标"
          />
          <FeatureCard
            icon={<BarChart3 className="h-6 w-6" />}
            title="波动率曲面"
            description="3D 可视化展示 Moneyness × 到期时间 × IV 的三维曲面，支持旋转/缩放交互"
          />
          <FeatureCard
            icon={<TrendingUp className="h-6 w-6" />}
            title="微笑曲线 & 期限结构"
            description="ECharts 渲染的 2D 图表，IV 百分位提示高低估水平"
          />
        </div>
      </section>
    </div>
  );
}

function UnderlyingCard({ underlying }: { underlying: Underlying }) {
  const symbolText = `${underlying.tsCode} · ${underlying.exchange}`;
  return (
    <Card className="hover:shadow-md transition-shadow">
      <CardHeader>
        <div className="flex items-start justify-between">
          <div>
            <CardTitle className="text-lg">{underlying.name}</CardTitle>
            <CardDescription className="text-xs">{symbolText}</CardDescription>
          </div>
          <span className="text-xs px-2 py-0.5 rounded-full bg-secondary text-secondary-foreground">
            {underlying.assetClass}
          </span>
        </div>
      </CardHeader>
      <CardContent className="space-y-2">
        <Link
          href={`/options-chain?underlying=${underlying.tsCode}`}
          className="flex items-center justify-between text-sm px-3 py-2 rounded-md hover:bg-muted transition-colors"
        >
          <span>查看期权链</span>
          <ChevronRight className="h-4 w-4" />
        </Link>
        <Link
          href={`/vol-surface?underlying=${underlying.tsCode}`}
          className="flex items-center justify-between text-sm px-3 py-2 rounded-md hover:bg-muted transition-colors"
        >
          <span>波动率分析</span>
          <ChevronRight className="h-4 w-4" />
        </Link>
      </CardContent>
    </Card>
  );
}

function FeatureCard({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2 text-primary">
          {icon}
          <CardTitle className="text-base">{title}</CardTitle>
        </div>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
}
