import { X, Check } from "lucide-react";

const before = [
  "Spreadsheets and scribbled notes",
  "WhatsApp threads for every tenant",
  "Messy folders and lost contracts",
];

const after = [
  "Single system for all properties",
  "Automated reminders and deadlines",
  "Shared access for co-landlords and agents",
];

export function ProblemSolution() {
  return (
    <section className="py-20 md:py-28 bg-background" aria-labelledby="problem-solution-heading">
      <div className="mx-auto max-w-6xl px-6">
        <h2 id="problem-solution-heading" className="font-heading text-3xl font-bold text-center text-primary mb-16">
          Before Daryva vs After Daryva
        </h2>
        <div className="grid gap-12 md:grid-cols-2">
          <div className="rounded-2xl border border-border bg-card p-8 shadow-card">
            <h3 className="font-heading text-lg font-semibold text-text-primary flex items-center gap-2">
              <X className="h-5 w-5 text-red-500" aria-hidden />
              Before Daryva
            </h3>
            <ul className="mt-6 space-y-4">
              {before.map((item) => (
                <li key={item} className="flex gap-3 text-text-muted">
                  <span className="text-red-400">•</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <div className="rounded-2xl border-2 border-accent bg-card p-8 shadow-card">
            <h3 className="font-heading text-lg font-semibold text-text-primary flex items-center gap-2">
              <Check className="h-5 w-5 text-accent" aria-hidden />
              After Daryva
            </h3>
            <ul className="mt-6 space-y-4">
              {after.map((item) => (
                <li key={item} className="flex gap-3 text-text-muted">
                  <span className="text-accent">•</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  );
}
