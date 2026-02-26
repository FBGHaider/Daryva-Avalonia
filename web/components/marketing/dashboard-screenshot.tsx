import Image from "next/image";
import { cn } from "@/lib/utils";

const dashboardImageSrc = "/images/dashboard-screenshot.png";

interface DashboardScreenshotProps {
  className?: string;
}

export function DashboardScreenshot({ className }: DashboardScreenshotProps) {
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
      <div className="relative aspect-video min-h-[280px] bg-border/20">
        <Image
          src={dashboardImageSrc}
          alt="Daryva dashboard showing houses, tenants, rent and quick actions"
          fill
          className="object-contain object-top"
          sizes="(max-width: 768px) 100vw, 560px"
          priority
        />
      </div>
    </div>
  );
}
