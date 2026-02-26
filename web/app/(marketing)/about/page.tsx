import type { Metadata } from "next";
import { Target, Eye, Zap, HeadphonesIcon } from "lucide-react";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "About",
  description: "Built in Cambridge, UK. Daryva exists to give landlords clarity and control over their rentals.",
  openGraph: { title: "About | " + site.name, description: "Our mission and values." },
};

const values = [
  { icon: Eye, name: "Clarity", description: "One place to see everything. No spreadsheets, no guesswork." },
  { icon: Target, name: "Trust", description: "We build for the long term. Your data and your time matter." },
  { icon: Zap, name: "Speed", description: "Quick setup, fast decisions. Get value from day one." },
  { icon: HeadphonesIcon, name: "Support", description: "Real support when you need it. We are here to help." },
];

export default function AboutPage() {
  return (
    <div className="bg-background">
      <section className="mx-auto max-w-6xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-4">About Daryva</h1>
        <p className="text-lg text-text-muted max-w-2xl mb-8">
          Built in Cambridge, UK. We started Daryva because we saw landlords juggling spreadsheets, WhatsApp, and messy folders. We wanted to build a calm, organised system that gives you clarity and control.
        </p>
        <p className="text-text-muted max-w-2xl mb-16">
          Our mission is simple: help you run your rentals like a business. Track tenants, rent, expenses, and documents in one place. Share access with co-landlords or agents without the chaos. Never miss a deadline.
        </p>
        <h2 className="font-heading text-2xl font-bold text-primary mb-8">What we care about</h2>
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {values.map(({ icon: Icon, name, description }) => (
            <div key={name} className="rounded-2xl border border-border bg-card p-6 shadow-card">
              <Icon className="h-8 w-8 text-accent mb-3" aria-hidden />
              <h3 className="font-heading font-semibold text-primary mb-2">{name}</h3>
              <p className="text-sm text-text-muted">{description}</p>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
