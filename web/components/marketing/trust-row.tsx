import { Shield, Users, Zap, FolderOpen, TrendingUp } from "lucide-react";

const items = [
  { icon: Shield, text: "Built for UK landlords" },
  { icon: Users, text: "Secure multi-user collaboration" },
  { icon: Zap, text: "Fast setup" },
  { icon: FolderOpen, text: "Organised documents" },
  { icon: TrendingUp, text: "Clear profit picture" },
];

export function TrustRow() {
  return (
    <section className="border-y border-border bg-card py-8" aria-label="Trust indicators">
      <div className="mx-auto max-w-6xl px-6">
        <ul className="flex flex-wrap items-center justify-center gap-x-10 gap-y-6">
          {items.map(({ icon: Icon, text }) => (
            <li key={text} className="flex items-center gap-3 text-text-muted">
              <Icon className="h-5 w-5 shrink-0 text-accent" aria-hidden />
              <span className="text-sm font-medium">{text}</span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
