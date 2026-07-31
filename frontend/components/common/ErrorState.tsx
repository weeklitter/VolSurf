// components/common/ErrorState.tsx

import { AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";

interface ErrorStateProps {
  message: string;
  className?: string;
  onRetry?: () => void;
}

export function ErrorState({ message, className, onRetry }: ErrorStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center min-h-[16rem] text-destructive gap-3",
        className
      )}
    >
      <AlertCircle className="h-10 w-10" />
      <p className="text-sm">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="text-sm text-primary hover:underline"
        >
          重试
        </button>
      )}
    </div>
  );
}
