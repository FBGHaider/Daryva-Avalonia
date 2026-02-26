import Link from "next/link";
import { site } from "@/lib/site";
import { brand } from "@/lib/brand";

const footerLinks = {
  product: [
    { href: "/product", label: "Features" },
    { href: "/pricing", label: "Pricing" },
    { href: "/demo", label: "Demo" },
  ],
  company: [
    { href: "/about", label: "About" },
    { href: "/contact", label: "Contact" },
    { href: "/security", label: "Security" },
  ],
  legal: [
    { href: "/legal/privacy", label: "Privacy" },
    { href: "/legal/terms", label: "Terms" },
  ],
};

export function Footer() {
  return (
    <footer className="border-t border-border bg-card" role="contentinfo">
      <div className="mx-auto max-w-6xl px-6 py-12">
        <div className="grid grid-cols-2 gap-8 md:grid-cols-4">
          <div className="col-span-2 md:col-span-1">
            <Link href="/" className="inline-flex items-center gap-2 focus:opacity-90">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={brand.iconSource}
                alt=""
                width={24}
                height={24}
                className="h-6 w-6 shrink-0 object-contain"
              />
              <span className="font-heading text-lg font-semibold text-primary tracking-tight">
                {site.name}
              </span>
            </Link>
            <p className="mt-3 text-sm text-text-muted">{site.footerTagline}</p>
          </div>
          <div>
            <h4 className="text-sm font-semibold text-text-primary">Product</h4>
            <ul className="mt-3 space-y-2">
              {footerLinks.product.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="text-sm text-text-muted hover:text-accent-blue">
                    {item.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h4 className="text-sm font-semibold text-text-primary">Company</h4>
            <ul className="mt-3 space-y-2">
              {footerLinks.company.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="text-sm text-text-muted hover:text-accent-blue">
                    {item.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        </div>
        <div className="mt-10 flex flex-col gap-4 border-t border-border pt-8 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex gap-6">
            {footerLinks.legal.map((item) => (
              <Link key={item.href} href={item.href} className="text-sm text-text-muted hover:text-accent-blue">
                {item.label}
              </Link>
            ))}
          </div>
          <p className="text-sm text-text-muted">
            © {new Date().getFullYear()} {site.name}. All rights reserved.
          </p>
        </div>
      </div>
    </footer>
  );
}
