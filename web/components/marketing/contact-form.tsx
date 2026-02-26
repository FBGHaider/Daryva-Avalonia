"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

export function ContactForm() {
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setLoading(true);
    setStatus("idle");
    const form = e.currentTarget;
    const data = new FormData(form);
    const payload = Object.fromEntries(data);
    // TODO: Wire to Resend or your email service
    console.log("Contact form submitted:", payload);
    await new Promise((r) => setTimeout(r, 600));
    setLoading(false);
    setStatus("success");
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" aria-label="Contact form">
      <div>
        <label htmlFor="contact-name" className="block text-sm font-medium text-text-primary mb-1">Name</label>
        <Input id="contact-name" name="name" required placeholder="Your name" />
      </div>
      <div>
        <label htmlFor="contact-email" className="block text-sm font-medium text-text-primary mb-1">Email</label>
        <Input id="contact-email" name="email" type="email" required placeholder="you@example.com" />
      </div>
      <div>
        <label htmlFor="contact-message" className="block text-sm font-medium text-text-primary mb-1">Message</label>
        <Textarea id="contact-message" name="message" required placeholder="How can we help?" rows={5} />
      </div>
      {status === "success" && <p className="text-sm text-accent" role="status">Thanks. We will get back to you soon.</p>}
      <Button type="submit" disabled={loading}>{loading ? "Sending..." : "Send message"}</Button>
    </form>
  );
}
