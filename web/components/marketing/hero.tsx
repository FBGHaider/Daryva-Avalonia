import Link from "next/link";
import { Button } from "@/components/ui/button";
import { site } from "@/lib/site";
import { ScreenshotPlaceholder } from "./screenshot-placeholder";

export function Hero() {
  return (
    <section className="relative overflow-hidden bg-background" aria-labelledby="hero-heading">
      {/* Subtle brand gradient blob behind screenshot area (5–8% opacity, logo-inspired) */}
      <div className="absolute top-1/4 right-0 w-[80%] max-w-2xl h-[70%] max-h-[500px] -translate-y-1/2 pointer-events-none" aria-hidden>
        <div className="absolute inset-0 rounded-full blur-3xl opacity-[0.07]" style={{ background: "radial-gradient(circle, #21C58E 0%, #3B82F6 40%, transparent 70%)" }} />
      </div>
      <div className="mx-auto relative max-w-6xl px-6 py-20 md:py-28 lg:py-32">
        <div className="grid gap-12 lg:grid-cols-2 lg:gap-16 lg:items-center">
          <div>
            <h1 id="hero-heading" className="font-heading text-4xl font-bold tracking-tight text-primary sm:text-5xl lg:text-6xl">
              Run your rentals like a business.
            </h1>
            <p className="mt-6 text-lg text-text-muted max-w-xl">
              Track tenants, rent, expenses, documents, and reminders in one calm dashboard built for landlords who want clarity.
            </p>
            <div className="mt-8 flex flex-wrap gap-4">
              <Button variant="accent" size="lg" asChild>
                <a href={site.appSignupUrl} target="_blank" rel="noopener noreferrer">Start free trial</a>
              </Button>
              <Button variant="outline" size="lg" asChild>
                <Link href="/demo">Book a demo</Link>
              </Button>
            </div>
            <p className="mt-6 text-sm text-text-muted">No credit card required. Cancel anytime. Built for UK landlords.</p>
          </div>
          <div className="relative">
            <ScreenshotPlaceholder label="Dashboard screenshot" className="max-w-xl mx-auto" />
          </div>
        </div>
      </div>
    </section>
  );
}
