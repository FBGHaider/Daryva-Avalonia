import type { Metadata } from "next";
import { ContactForm } from "@/components/marketing/contact-form";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Contact",
  description: "Get in touch with the Daryva team. We typically respond within one business day.",
  openGraph: { title: "Contact | " + site.name, description: "Contact Daryva support and sales." },
};

export default function ContactPage() {
  return (
    <div className="bg-background">
      <section className="mx-auto max-w-2xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-4">Contact us</h1>
        <p className="text-lg text-text-muted mb-8">
          Have a question or want to say hello? Send us a message and we will get back to you.
        </p>
        <p className="text-text-muted mb-6">
          Support: <a href={"mailto:" + site.supportEmail} className="text-accent-blue underline underline-offset-2 hover:text-accent-blue/80">{site.supportEmail}</a>
        </p>
        <p className="text-sm text-text-muted mb-8">We aim to respond within one business day.</p>
        <div className="rounded-2xl border border-border bg-card p-8 shadow-card">
          <ContactForm />
        </div>
      </section>
    </div>
  );
}
