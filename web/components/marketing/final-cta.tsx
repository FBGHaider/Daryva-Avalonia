import Link from "next/link";
import { Button } from "@/components/ui/button";
import { site } from "@/lib/site";

export function FinalCta() {
  return (
    <section className="py-20 md:py-28 bg-primary text-white" aria-labelledby="final-cta-heading">
      <div className="mx-auto max-w-3xl px-6 text-center">
        <h2 id="final-cta-heading" className="font-heading text-3xl font-bold mb-4">
          Start your free trial
        </h2>
        <p className="text-white/80 mb-8">
          No credit card required. Get clarity on your rentals in minutes.
        </p>
        <div className="flex flex-wrap justify-center gap-4">
          <Button variant="accent" size="lg" asChild>
            <a href={site.appSignupUrl} target="_blank" rel="noopener noreferrer">
              Start free trial
            </a>
          </Button>
          <Button variant="outline" size="lg" className="border-white text-white hover:bg-white/10" asChild>
            <Link href="/demo">Book a demo</Link>
          </Button>
        </div>
      </div>
    </section>
  );
}
