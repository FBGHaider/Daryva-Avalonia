import { Hero } from "@/components/marketing/hero";
import { TrustRow } from "@/components/marketing/trust-row";
import { ProblemSolution } from "@/components/marketing/problem-solution";
import { FeatureHighlights } from "@/components/marketing/feature-highlights";
import { HowItWorks } from "@/components/marketing/how-it-works";
import { ScreenshotGallery } from "@/components/marketing/screenshot-gallery";
import { PricingPreview } from "@/components/marketing/pricing-preview";
import { FAQ } from "@/components/marketing/faq";
import { FinalCta } from "@/components/marketing/final-cta";

export default function HomePage() {
  return (
    <>
      <Hero />
      <TrustRow />
      <ProblemSolution />
      <FeatureHighlights />
      <HowItWorks />
      <ScreenshotGallery />
      <PricingPreview />
      <FAQ />
      <FinalCta />
    </>
  );
}
