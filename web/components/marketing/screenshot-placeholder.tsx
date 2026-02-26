import { cn } from "@/lib/utils";

interface ScreenshotPlaceholderProps {
  label: string;
  className?: string;
}

export function ScreenshotPlaceholder({ label, className }: ScreenshotPlaceholderProps) {
  return (
    <div
      className={cn(
        "rounded-2xl border border-border bg-card overflow-hidden shadow-card",
        className
      )}
      aria-hidden
    >
      <div className="flex items-center gap-2 border-b border-border bg-background px-4 py-3">
        <div className="flex gap-2">
          <div className="h-3 w-3 rounded-full bg-[#E5E7EB]" />
          <div className="h-3 w-3 rounded-full bg-[#E5E7EB]" />
          <div className="h-3 w-3 rounded-full bg-[#E5E7EB]" />
        </div>
        <div className="flex-1 flex justify-center">
          <div className="rounded-lg bg-border h-6 w-32" />
        </div>
      </div>
      <div className="aspect-video flex items-center justify-center bg-gradient-to-br from-background to-border/30 p-8">
        <div className="text-center">
          <div className="mx-auto h-24 w-24 rounded-2xl bg-border/60 mb-4" />
          <p className="text-sm font-medium text-text-muted">{label}</p>
        </div>
      </div>
    </div>
  );
}
