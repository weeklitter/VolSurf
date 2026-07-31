// components/common/Disclaimer.tsx
// 全局免责声明 - 出现在每个页面 footer

import { AlertTriangle } from "lucide-react";

export function Disclaimer() {
  return (
    <footer className="border-t bg-muted/30 mt-12">
      <div className="container py-6">
        <div className="flex items-start gap-3 text-sm text-muted-foreground">
          <AlertTriangle className="h-5 w-5 text-yellow-600 flex-shrink-0 mt-0.5" />
          <div className="space-y-1">
            <p className="font-semibold text-foreground">免责声明 / Disclaimer</p>
            <p>
              本网站所展示的所有数据（包括但不限于期权行情、隐含波动率 IV、希腊值等）
              仅供学习研究使用，不构成任何投资建议。期权交易存在高风险，可能导致全部本金损失。
            </p>
            <p>
              数据来源：Tushare / AKShare。本站不保证数据的准确性、完整性和及时性。
              使用本网站数据进行的任何投资决策，所产生的盈亏均由使用者自行承担。
            </p>
            <p className="text-xs">
              This site is for educational and research purposes only.
              Nothing here constitutes investment advice. Options trading involves
              substantial risk. Past performance does not guarantee future results.
            </p>
          </div>
        </div>
        <div className="mt-4 pt-4 border-t text-xs text-muted-foreground flex justify-between">
          <span>© {new Date().getFullYear()} VolSurf</span>
          <span>Built with Next.js + TypeScript + TailwindCSS</span>
        </div>
      </div>
    </footer>
  );
}
