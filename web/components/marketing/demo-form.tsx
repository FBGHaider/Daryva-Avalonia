"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

export function DemoForm() {
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setLoading(true);
    setStatus("idle");
    const form = e.currentTarget;
    const data = new FormData(form);
    const payload = Object.fromEntries(data);
    // TODO: Wire to Resend or CRM
    console.log("Demo form submitted:", payload);
    await new Promise((r) => setTimeout(r, 600));
    setLoading(false);
    setStatus("success");
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" aria-label="Book a demo form">
      <div>
        <label htmlFor="demo-name" className="block text-sm font-medium text-text-primary mb-1">Name</label>
        <Input id="demo-name" name="name" required placeholder="Your name" />
      </div>
      <div>
        <label htmlFor="demo-email" className="block text-sm font-medium text-text-primary mb-1">Email</label>
        <Input id="demo-email" name="email" type="email" required placeholder="you@example.com" />
      </div>
      <div>
        <label htmlFor="demo-portfolio" className="block text-sm font-medium text-text-primary mb-1">Portfolio size</label>
        <Input id="demo-portfolio" name="portfolioSize" placeholder="e.g. 5 properties" />
      </div>
      <div>
        <label htmlFor="demo-message" className="block text-sm font-medium text-text-primary mb-1">Message</label>
        <Textarea id="demo-message" name="message" placeholder="Anything you would like us to focus on?" rows={4} />
      </div>
      {status === "success" && <p className="text-sm text-accent" role="status">Demo requested. We will be in touch to schedule.</p>}
      <Button type="submit" disabled={loading}>{loading ? "Sending..." : "Book a demo"}</Button>
    </form>
  );
}
