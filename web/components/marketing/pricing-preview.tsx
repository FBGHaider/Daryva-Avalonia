"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { site } from "@/lib/site";
import { Check } from "lucide-react";

const tiers = [
  { name: "Starter", monthlyPrice: 9, description: "Up to 3 properties", features: ["Rent tracking", "Document storage", "Email support"], popular: false },
  { name: "Pro", monthlyPrice: 19, description: "Up to 15 properties, collaboration, advanced reporting", features: ["Everything in Starter", "Co-landlord collaboration", "Advanced reporting", "Priority support"], popular: true },
  { name: "Agency", monthlyPrice: 49, description: "Unlimited properties, roles and permissions, audit logs", features: ["Everything in Pro", "Unlimited properties", "Roles and permissions", "Audit logs", "Dedicated support"], popular: false },
];

export function PricingPreview() {
  const [annual, setAnnual] = useState(false);
  return (
    <section className="py-20 md:py-28 bg-card border-y border-border" aria-labelledby="pricing-heading">
      <div className="mx-auto max-w-6xl px-6">
        <h2 id="pricing-heading" className="font-heading text-3xl font-bold text-center text-primary mb-4">Simple, transparent pricing</h2>
        <p className="text-center text-text-muted mb-8">Choose the plan that fits your portfolio. Upgrade or downgrade anytime.</p>
        <div className="flex justify-center items-center gap-3 mb-12">
          <span className="text-sm text-text-muted">Monthly</span>
          <Switch checked={annual} onCheckedChange={setAnnual} aria-label="Toggle annual billing" />
          <span className="text-sm text-text-primary font-medium">Annual</span>
          {annual && <Badge variant="accent" className="ml-1">2 months free</Badge>}
        </div>
        <div className="grid gap-8 md:grid-cols-3">
          {tiers.map((tier) => (
            <Card key={tier.name} className={"relative border-border " + (tier.popular ? "ring-2 ring-accent shadow-card" : "")}>
              {tier.popular && <div className="absolute -top-3 left-1/2 -translate-x-1/2"><Badge variant="accent">Most popular</Badge></div>}
              <CardHeader>
                <h3 className="font-heading text-xl font-semibold text-primary">{tier.name}</h3>
                <p className="text-text-muted text-sm">{tier.description}</p>
                <p className="pt-2"><span className="font-heading text-3xl font-bold text-primary">£{tier.monthlyPrice}</span><span className="text-text-muted">/mo</span></p>
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
          ))}
        </div>
      </div>
    </section>
  );
}
