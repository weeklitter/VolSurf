// app/layout.tsx
// 全局 layout：导航栏 + 免责声明 footer

import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/components/common/Providers";
import { NavBar } from "@/components/common/NavBar";
import { Disclaimer } from "@/components/common/Disclaimer";

export const metadata: Metadata = {
  title: "VolSurf - 期权波动率分析",
  description: "ETF 期权 / 股指期权的隐含波动率曲面、微笑曲线、期限结构分析平台",
  keywords: ["期权", "波动率", "IV", "BS", "Greeks", "隐含波动率"],
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="zh-CN" suppressHydrationWarning>
      <body className="min-h-screen flex flex-col antialiased">
        <Providers>
          <NavBar />
          <main className="flex-1">{children}</main>
          <Disclaimer />
        </Providers>
      </body>
    </html>
  );
}
