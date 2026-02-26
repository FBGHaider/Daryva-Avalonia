import { Home, UserPlus, BarChart3 } from "lucide-react";

const steps = [
  { icon: Home, title: "Add properties", description: "Enter your portfolio. One place for every property and tenancy." },
  { icon: UserPlus, title: "Invite co-landlord / agent", description: "Share access securely. Everyone sees the same up-to-date view." },
  { icon: BarChart3, title: "Track everything live", description: "Rent, expenses, documents, and reminders — all in one dashboard." },
];

export function HowItWorks() {
  return (
    <section className="py-20 md:py-28 bg-card border-y border-border" aria-labelledby="how-it-works-heading">
      <div className="mx-auto max-w-6xl px-6">
        <h2 id="how-it-works-heading" className="font-heading text-3xl font-bold text-center text-primary mb-16">
          How it works
        </h2>
        <div className="grid gap-12 md:grid-cols-3">
          {steps.map(({ icon: Icon, title, description }, i) => (
            <div key={title} className="relative text-center">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-primary text-white font-heading font-semibold text-lg">
                {i + 1}
              </div>
              <div className="mt-4 flex justify-center">
                <Icon className="h-8 w-8 text-accent" aria-hidden />
              </div>
              <h3 className="mt-4 font-heading text-lg font-semibold text-primary">{title}</h3>
              <p className="mt-2 text-text-muted">{description}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
