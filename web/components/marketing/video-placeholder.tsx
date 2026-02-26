import { Play } from "lucide-react";

export function VideoPlaceholder() {
  return (
    <div className="aspect-video rounded-2xl border border-border bg-gradient-to-br from-primary/5 to-border/30 flex items-center justify-center shadow-card" aria-hidden>
      <div className="flex h-20 w-20 items-center justify-center rounded-full bg-primary/90 text-white">
        <Play className="h-10 w-10 ml-1" fill="currentColor" />
      </div>
      <span className="sr-only">Demo video placeholder</span>
    </div>
  );
}
