import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Banknote, FileCheck, Bell } from "lucide-react";

const features = [
  {
    icon: Banknote,
    title: "Rent and payments",
    description: "Overdue tracking, payment ledger, and a clear view of what is due and what is paid.",
  },
  {
    icon: FileCheck,
    title: "Document vault",
    description: "Contracts, certificates, and key documents in one place. No more scattered folders.",
  },
  {
    icon: Bell,
    title: "Notifications and reminders",
    description: "Never miss a renewal or inspection. Get gentle nudges so deadlines do not slip.",
  },
];

export function FeatureHighlights() {
  return (
    <section className="py-20 md:py-28 bg-background" aria-labelledby="features-heading">
      <div className="mx-auto max-w-6xl px-6">
        <h2 id="features-heading" className="font-heading text-3xl font-bold text-center text-primary mb-4">
          Everything in one place
        </h2>
        <p className="text-center text-text-muted max-w-2xl mx-auto mb-16">
          Built for the way landlords actually work. Practical, clear, and easy to share with co-owners or agents.
        </p>
        <div className="grid gap-8 md:grid-cols-3">
          {features.map(({ icon: Icon, title, description }) => (
            <Card key={title} className="border-border">
              <CardHeader>
                <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-accent/10 text-accent mb-2">
                  <Icon className="h-6 w-6" aria-hidden />
                </div>
                <h3 className="font-heading text-xl font-semibold text-primary">{title}</h3>
              </CardHeader>
              <CardContent>
                <p className="text-text-muted">{description}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </section>
  );
}
