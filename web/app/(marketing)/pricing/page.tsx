"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { site } from "@/lib/site";
import { Check } from "lucide-react";

const tiers = [
  { name: "Starter", monthlyPrice: 9, annualPrice: 90, description: "Up to 3 properties", features: ["Rent tracking", "Document storage", "Email support"], popular: false },
  { name: "Pro", monthlyPrice: 19, annualPrice: 190, description: "Up to 15 properties", features: ["Everything in Starter", "Co-landlord collaboration", "Advanced reporting", "Priority support"], popular: true },
  { name: "Agency", monthlyPrice: 49, annualPrice: 490, description: "Unlimited properties", features: ["Everything in Pro", "Unlimited properties", "Roles and permissions", "Audit logs", "Dedicated support"], popular: false },
];

const pricingFaqs = [
  { q: "What is included?", a: "All plans include property and tenant management, rent tracking, document storage, and email support." },
  { q: "Free trial?", a: "Yes. Start with no credit card and explore before committing." },
  { q: "Fair use?", a: "We expect normal use. We will communicate before any change." },
];

export default function PricingPage() {
  const [annual, setAnnual] = useState(false);
  return (
    <div className="bg-background">
      <section className="mx-auto max-w-6xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-4">Pricing</h1>
        <p className="text-lg text-text-muted max-w-2xl mb-12">Simple tiers. No hidden fees.</p>
        <div className="flex justify-center items-center gap-3 mb-12">
          <span className="text-sm text-text-muted">Monthly</span>
          <Switch checked={annual} onCheckedChange={setAnnual} aria-label="Annual billing" />
          <span className="text-sm text-text-primary font-medium">Annual</span>
          {annual && <Badge variant="accent" className="ml-1">2 months free</Badge>}
        </div>
        <div className="grid gap-8 md:grid-cols-3">
          {tiers.map((tier) => {
            const price = annual ? tier.annualPrice : tier.monthlyPrice;
            const period = annual ? "year" : "month";
            return (
              <Card key={tier.name} className={"relative border-border " + (tier.popular ? "ring-2 ring-accent shadow-card" : "")}>
                {tier.popular && <div className="absolute -top-3 left-1/2 -translate-x-1/2"><Badge variant="accent">Most popular</Badge></div>}
                <CardHeader>
                  <h2 className="font-heading text-xl font-semibold text-primary">{tier.name}</h2>
                  <p className="text-text-muted text-sm">{tier.description}</p>
                  <p className="pt-2"><span className="font-heading text-3xl font-bold text-primary">£{price}</span><span className="text-text-muted">/{period}</span></p>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-3 mb-6">
                    {tier.features.map((f) => (
                      <li key={f} className="flex items-center gap-2 text-sm text-text-muted"><Check className="h-4 w-4 shrink-0 text-accent" />{f}</li>
                    ))}
                  </ul>
                  <Button variant={tier.popular ? "accent" : "primary"} className="w-full" asChild>
                    <a href={site.appSignupUrl} target="_blank" rel="noopener noreferrer">Start free trial</a>
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </div>
        <p className="mt-8 text-center text-sm text-text-muted">All prices GBP. Fair use applies.</p>
      </section>
      <section className="py-16 border-t border-border">
        <div className="mx-auto max-w-3xl px-6">
          <h2 className="font-heading text-2xl font-bold text-center text-primary mb-8">Pricing FAQ</h2>
          <ul className="space-y-6">
            {pricingFaqs.map((item) => (
              <li key={item.q}><h3 className="font-heading font-semibold text-text-primary">{item.q}</h3><p className="mt-1 text-text-muted">{item.a}</p></li>
            ))}
          </ul>
        </div>
      </section>
    </div>
  );
}
