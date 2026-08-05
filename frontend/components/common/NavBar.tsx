// components/common/NavBar.tsx
// 全局顶部导航栏 — 分组导航（股票 | 期权下拉 | 期货预留）

"use client";

import { useState, useRef, useEffect } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Activity,
  BarChart3,
  ChevronDown,
  Home,
  LineChart,
  TrendingUp,
} from "lucide-react";

// ── 普通导航链接 ──
function NavLink({
  href,
  icon: Icon,
  label,
  active,
}: {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  active: boolean;
}) {
  return (
    <Link
      href={href}
      className={`flex items-center gap-1.5 transition-colors ${
        active
          ? "text-foreground font-medium"
          : "text-muted-foreground hover:text-foreground"
      }`}
    >
      <Icon className="h-4 w-4" />
      <span className="hidden sm:inline">{label}</span>
    </Link>
  );
}

// ── 下拉菜单 ──
function DropdownMenu({
  label,
  icon: Icon,
  items,
  active,
}: {
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  items: { href: string; label: string; icon: React.ComponentType<{ className?: string }> }[];
  active: boolean;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((v) => !v)}
        className={`flex items-center gap-1.5 transition-colors ${
          active
            ? "text-foreground font-medium"
            : "text-muted-foreground hover:text-foreground"
        }`}
      >
        <Icon className="h-4 w-4" />
        <span className="hidden sm:inline">{label}</span>
        <ChevronDown
          className={`h-3 w-3 transition-transform ${open ? "rotate-180" : ""}`}
        />
      </button>
      {open && (
        <div className="absolute top-full left-0 mt-1 w-44 rounded-md border bg-popover p-1 shadow-md z-50">
          {items.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              onClick={() => setOpen(false)}
              className="flex items-center gap-2 rounded-sm px-3 py-2 text-sm text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

export function NavBar() {
  const pathname = usePathname();
  const stockActive = pathname?.startsWith("/stocks") ?? false;
  const optionsActive =
    pathname?.startsWith("/options-chain") ||
    pathname?.startsWith("/vol-surface") ||
    false;

  return (
    <header className="border-b bg-background sticky top-0 z-50">
      <div className="container flex h-14 items-center justify-between">
        <div className="flex items-center gap-4 sm:gap-8">
          <Link href="/" className="flex items-center gap-2 font-semibold shrink-0">
            <Activity className="h-5 w-5 text-primary" />
            <span>VolSurf</span>
          </Link>
          <nav className="flex items-center gap-4 sm:gap-6 text-sm">
            <NavLink href="/" icon={Home} label="首页" active={pathname === "/"} />
            <NavLink
              href="/stocks"
              icon={TrendingUp}
              label="股票"
              active={stockActive}
            />
            <DropdownMenu
              label="期权"
              icon={LineChart}
              active={optionsActive}
              items={[
                { href: "/options-chain", label: "期权链", icon: LineChart },
                { href: "/vol-surface", label: "波动率分析", icon: BarChart3 },
              ]}
            />
            {/* 期货 — 灰色禁用，预留 */}
            <span className="flex items-center gap-1.5 text-muted-foreground/40 cursor-not-allowed select-none">
              <BarChart3 className="h-4 w-4" />
              <span className="hidden sm:inline">期货</span>
            </span>
          </nav>
        </div>
        <div className="text-xs text-muted-foreground hidden sm:block">
          量化分析平台 v0.2.0
        </div>
      </div>
    </header>
  );
}
