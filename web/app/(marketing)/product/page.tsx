import type { Metadata } from "next";
import { ScreenshotPlaceholder } from "@/components/marketing/screenshot-placeholder";
import { Badge } from "@/components/ui/badge";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: "Product",
  description: "Dashboard, tenants, rent, expenses, documents, notifications, and collaboration. See what Daryva offers.",
  openGraph: { title: "Product | " + site.name, description: "Features and capabilities of Daryva property management." },
};

const sections = [
  { title: "Dashboard", description: "One view of your portfolio. Properties, rent status, and upcoming tasks at a glance.", label: "Dashboard" },
  { title: "Houses and tenants", description: "Manage properties and tenancies. Contact details, lease dates, and notes in one place.", label: "Tenants" },
  { title: "Rent and payments", description: "Track rent due and received. Overdue highlights and a clear ledger for each tenancy.", label: "Payments" },
  { title: "Expenses", description: "Log and categorise expenses. See where money goes and keep records for tax and reporting.", label: "Expenses" },
  { title: "Documents", description: "Store contracts, certificates, and key documents. Find what you need without digging through folders.", label: "Documents" },
  { title: "Notifications", description: "Reminders for renewals, inspections, and deadlines. Never miss an important date.", label: "Notifications" },
  { title: "Collaboration", description: "Invite co-landlords or agents. Shared access with the right level of visibility and control.", label: "Collaboration" },
  { title: "Reporting", description: "Insights and reports on your portfolio. (More advanced reporting and audit logs coming soon.)", label: "Reporting", comingSoon: true },
];

export default function ProductPage() {
  return (
    <div className="bg-background">
      <section className="mx-auto max-w-6xl px-6 py-16 md:py-24">
        <h1 className="font-heading text-4xl font-bold text-primary mb-4">Product</h1>
        <p className="text-lg text-text-muted max-w-2xl">A calm, organised system for your rentals. Here is what is inside.</p>
      </section>
      {sections.map((section, i) => (
        <section key={section.title} className={"py-12 md:py-16 " + (i % 2 === 1 ? "bg-card border-y border-border" : "")}>
          <div className="mx-auto max-w-6xl px-6">
            <div className="grid gap-12 md:grid-cols-2 md:gap-16 items-center">
              <div className={i % 2 === 1 ? "md:order-2" : ""}>
                <div className="flex items-center gap-2 mb-2">
                  <h2 className="font-heading text-2xl font-bold text-primary">{section.title}</h2>
                  {section.comingSoon && <Badge variant="secondary">Coming soon</Badge>}
                </div>
                <p className="text-text-muted">{section.description}</p>
              </div>
              <div className={i % 2 === 1 ? "md:order-1" : ""}>
                <ScreenshotPlaceholder label={section.label} />
              </div>
            </div>
          </div>
        </section>
      ))}
    </div>
  );
}
