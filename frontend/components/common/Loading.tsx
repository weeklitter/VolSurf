// components/common/Loading.tsx

import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";

interface LoadingProps {
  className?: string;
  message?: string;
}

export function Loading({ className, message = "加载中..." }: LoadingProps) {
  return (
    <div
      className={cn(
        "flex items-center justify-center min-h-[16rem] text-muted-foreground",
        className
      )}
    >
      <Loader2 className="h-8 w-8 animate-spin" />
      <span className="ml-3">{message}</span>
    </div>
  );
}
