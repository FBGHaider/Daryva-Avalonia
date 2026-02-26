import type { Metadata } from "next";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Privacy Policy",
  description: "Privacy policy for Daryva. How we collect, use, and protect your data.",
  robots: { index: true, follow: true },
};

export default function PrivacyPage() {
  return (
    <div className="bg-background">
      <article className="mx-auto max-w-3xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-8">Privacy Policy</h1>
        <p className="text-text-muted mb-6">Last updated: {new Date().toLocaleDateString("en-GB")}. This is a placeholder. Please have a qualified professional draft your privacy policy.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">1. Introduction</h2>
        <p className="text-text-muted">This privacy policy describes how {site.name} ({site.url}) collects, uses, and protects your personal data when you use our website and services. We are committed to protecting your privacy and complying with applicable data protection laws including the UK GDPR.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">2. Data we collect</h2>
        <p className="text-text-muted">We may collect: account and profile information; property and tenancy data you enter; payment and billing details; usage and log data; and communications you send to us. We do not sell your data to third parties.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">3. How we use your data</h2>
        <p className="text-text-muted">We use your data to provide and improve our services, process payments, send important notices, and comply with legal obligations. We may use analytics to improve our product; you can control cookie preferences where offered.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">4. Data retention and security</h2>
        <p className="text-text-muted">We retain your data for as long as your account is active or as needed to provide services and comply with law. We use appropriate technical and organisational measures to protect your data.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">5. Your rights</h2>
        <p className="text-text-muted">You may have rights to access, correct, delete, or port your data, and to object to or restrict processing. Contact us at {site.supportEmail} to exercise these rights.</p>
        <h2 className="font-heading text-xl font-semibold text-primary mt-8 mb-2">6. Contact</h2>
        <p className="text-text-muted">For privacy-related questions, contact {site.supportEmail}. This policy may be updated; we will notify you of material changes.</p>
      </article>
    </div>
  );
}
