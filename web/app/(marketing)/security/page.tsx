import type { Metadata } from "next";
import { Shield, Lock, Database, Server } from "lucide-react";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Security",
  description: "How Daryva keeps your data secure. HTTPS, authentication, isolation, and GDPR-friendly practices.",
  openGraph: { title: "Security | " + site.name, description: "Security and data protection at Daryva." },
};

const points = [
  { icon: Shield, title: "HTTPS everywhere", description: "All traffic to and from Daryva is encrypted in transit. We use industry-standard TLS." },
  { icon: Lock, title: "Authentication", description: "Secure sign-in with strong password requirements. We support best practices for session handling." },
  { icon: Database, title: "Organisation data isolation", description: "Your data is strictly isolated per organisation. No cross-tenant access." },
  { icon: Server, title: "Backups and availability", description: "We run reliable infrastructure with regular backups so your data is safe and available when you need it." },
];

export default function SecurityPage() {
  return (
    <div className="bg-background">
      <section className="mx-auto max-w-6xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-4">Security</h1>
        <p className="text-lg text-text-muted max-w-2xl mb-12">
          We take security seriously. Here is how we protect your data and keep your portfolio information safe.
        </p>
        <div className="grid gap-8 md:grid-cols-2">
          {points.map(({ icon: Icon, title, description }) => (
            <div key={title} className="rounded-2xl border border-border bg-card p-8 shadow-card">
              <Icon className="h-10 w-10 text-accent mb-4" aria-hidden />
              <h2 className="font-heading text-xl font-semibold text-primary mb-2">{title}</h2>
              <p className="text-text-muted">{description}</p>
            </div>
          ))}
        </div>
        <div className="mt-16 rounded-2xl border border-border bg-card p-8 shadow-card">
          <h2 className="font-heading text-xl font-semibold text-primary mb-4">GDPR and data protection</h2>
          <p className="text-text-muted">
            We process data in line with UK and EU data protection requirements. You retain control of your data; we do not sell it to third parties. Our privacy policy sets out how we collect, use, and protect your information. If you have questions, contact us at {site.supportEmail}.
          </p>
        </div>
      </section>
    </div>
  );
}
