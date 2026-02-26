import type { Metadata } from "next";
import { Button } from "@/components/ui/button";
import { VideoPlaceholder } from "@/components/marketing/video-placeholder";
import { DemoForm } from "@/components/marketing/demo-form";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Demo",
  description: "Watch a short demo of Daryva or book a live walkthrough.",
  openGraph: { title: "Demo | " + site.name, description: "See Daryva in action or book a demo." },
};

export default function DemoPage() {
  return (
    <div className="bg-background">
      <section className="mx-auto max-w-6xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-4">See Daryva in action</h1>
        <p className="text-lg text-text-muted max-w-2xl mb-12">Watch a short overview or book a live demo.</p>
        <div className="aspect-video max-w-4xl mb-16">
          <VideoPlaceholder />
        </div>
        <div className="grid gap-12 md:grid-cols-2">
          <div>
            <h2 className="font-heading text-2xl font-bold text-primary mb-4">Book a demo</h2>
            <p className="text-text-muted mb-6">We will set up a short call to walk you through Daryva.</p>
            <DemoForm />
          </div>
          <div>
            <h2 className="font-heading text-2xl font-bold text-primary mb-4">Prefer to explore yourself?</h2>
            <p className="text-text-muted mb-6">Start a free trial. No credit card required.</p>
            <Button variant="accent" size="lg" asChild>
              <a href={site.appSignupUrl} target="_blank" rel="noopener noreferrer">Start free trial</a>
            </Button>
          </div>
        </div>
      </section>
    </div>
  );
}
