// components/common/NavBar.tsx
// 全局顶部导航栏

import Link from "next/link";
import { Activity, BarChart3, LineChart, Home } from "lucide-react";

export function NavBar() {
  return (
    <header className="border-b bg-background sticky top-0 z-50">
      <div className="container flex h-14 items-center justify-between">
        <div className="flex items-center gap-8">
          <Link href="/" className="flex items-center gap-2 font-semibold">
            <Activity className="h-5 w-5 text-primary" />
            <span>VolSurf</span>
          </Link>
          <nav className="flex items-center gap-6 text-sm">
            <Link
              href="/"
              className="flex items-center gap-1.5 text-muted-foreground hover:text-foreground transition-colors"
            >
              <Home className="h-4 w-4" />
              首页
            </Link>
            <Link
              href="/options-chain"
              className="flex items-center gap-1.5 text-muted-foreground hover:text-foreground transition-colors"
            >
              <LineChart className="h-4 w-4" />
              期权链
            </Link>
            <Link
              href="/vol-surface"
              className="flex items-center gap-1.5 text-muted-foreground hover:text-foreground transition-colors"
            >
              <BarChart3 className="h-4 w-4" />
              波动率分析
            </Link>
          </nav>
        </div>
        <div className="text-xs text-muted-foreground">期权分析平台 v0.1.0</div>
      </div>
    </header>
  );
}
