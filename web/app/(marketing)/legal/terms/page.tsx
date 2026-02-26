import type { Metadata } from "next";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Terms of Service",
  description: "Terms of service for Daryva. Usage and legal terms.",
  robots: { index: true, follow: true },
};

export default function TermsPage() {
  return (
    <div className="bg-background">
      <article className="mx-auto max-w-3xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-8">Terms of Service</h1>
        <p className="text-text-muted mb-6">Last updated: {new Date().toLocaleDateString("en-GB")}. This is a placeholder. Please have a qualified professional draft your terms.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">1. Agreement</h2>
        <p className="text-text-muted">By using {site.name} ({site.url}) and our services, you agree to these terms. If you are using the service on behalf of an organisation, you represent that you have authority to bind that organisation.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">2. Service description</h2>
        <p className="text-text-muted">Daryva provides property management software for landlords and agencies. We reserve the right to modify or discontinue features with reasonable notice where practicable.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">3. Account and use</h2>
        <p className="text-text-muted">You are responsible for maintaining the security of your account and for all activity under it. You must use the service in compliance with applicable laws and not misuse or attempt to gain unauthorised access.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">4. Payment and cancellation</h2>
        <p className="text-text-muted">Paid plans are billed as described on our pricing page. You may cancel in accordance with your plan; refunds are subject to our policy at the time of cancellation.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">5. Limitation of liability</h2>
        <p className="text-text-muted">To the extent permitted by law, our liability is limited. We do not exclude liability for death or personal injury caused by our negligence, fraud, or other liability that cannot be excluded by law.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">6. Contact</h2>
        <p className="text-text-muted">For questions about these terms, contact {site.supportEmail}. We may update these terms and will notify you of material changes.</p>
      </article>
    </div>
  );
}
