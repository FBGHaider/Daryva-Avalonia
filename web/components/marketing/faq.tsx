"use client";

import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";

const faqs = [
  { q: "Is my data safe?", a: "Yes. We use industry-standard encryption (HTTPS, secure storage) and do not sell your data. Your information is isolated per organisation." },
  { q: "Can I collaborate with a co-landlord or agent?", a: "Yes. Pro and Agency plans include secure multi-user access. Invite others to your portfolio with the right level of access." },
  { q: "How many properties can I add?", a: "Starter: up to 3. Pro: up to 15. Agency: unlimited. You can upgrade or downgrade as your portfolio changes." },
  { q: "Can I cancel anytime?", a: "Yes. No long-term lock-in. Cancel from your account settings and your data remains exportable." },
  { q: "Is Daryva suitable for UK landlords?", a: "Yes. Daryva is built with UK landlords in mind: rent periods, compliance reminders, and terminology that matches how you work." },
  { q: "What support do you offer?", a: "All plans include email support. Pro and Agency get priority support. We aim to respond within one business day." },
];

export function FAQ() {
  return (
    <section className="py-20 md:py-28 bg-background" aria-labelledby="faq-heading">
      <div className="mx-auto max-w-3xl px-6">
        <h2 id="faq-heading" className="font-heading text-3xl font-bold text-center text-primary mb-12">Frequently asked questions</h2>
        <Accordion type="single" collapsible className="w-full">
          {faqs.map((item) => (
            <AccordionItem key={item.q} value={item.q}>
              <AccordionTrigger>{item.q}</AccordionTrigger>
              <AccordionContent>{item.a}</AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </div>
    </section>
  );
}
