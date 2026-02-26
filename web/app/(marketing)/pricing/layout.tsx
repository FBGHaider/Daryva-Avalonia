import type { Metadata } from "next";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Pricing",
  description: "Simple pricing for UK landlords and agencies. Starter, Pro, and Agency plans.",
  openGraph: { title: "Pricing | " + site.name, description: "Simple pricing for Daryva." },
};

export default function PricingLayout({ children }: { children: React.ReactNode }) {
  return children;
}
