"use client";

import Link from "next/link";
import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { site } from "@/lib/site";
import { brand } from "@/lib/brand";
import { Menu, X } from "lucide-react";

const navLinks = [
  { href: "/product", label: "Product" },
  { href: "/pricing", label: "Pricing" },
  { href: "/demo", label: "Demo" },
  { href: "/security", label: "Security" },
  { href: "/about", label: "About" },
  { href: "/contact", label: "Contact" },
];

export function Header() {
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const headerClass = scrolled
    ? "bg-card/90 backdrop-blur-md border-b border-border shadow-soft"
    : "bg-transparent";

  return (
    <header className={"fixed top-0 left-0 right-0 z-50 transition-all duration-300 " + headerClass} role="banner">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6 md:px-8">
        <Link
          href="/"
          className="flex items-center gap-3 py-2 pr-2 -ml-2 rounded-lg hover:opacity-90 transition-opacity min-h-[44px]"
          aria-label={site.name + " home"}
        >
          {/* Daryva_icon.ico - use img so .ico loads correctly (next/image doesn't optimize ico) */}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={brand.iconSource}
            alt=""
            width={32}
            height={32}
            className="h-7 w-7 sm:h-8 sm:w-8 shrink-0 object-contain"
          />
          <span className="font-heading text-xl font-semibold text-primary tracking-tight">
            {site.name}
          </span>
        </Link>

        <nav className="hidden md:flex items-center gap-8" aria-label="Main navigation">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className="text-sm font-medium text-text-primary hover:text-accent-blue transition-colors"
            >
              {link.label}
            </Link>
          ))}
        </nav>

        <div className="hidden md:flex items-center gap-3">
          <Button variant="ghost" size="sm" asChild>
            <a href={site.appUrl} target="_blank" rel="noopener noreferrer">
              Sign in
            </a>
          </Button>
          <Button variant="accent" size="sm" asChild>
            <a href={site.appSignupUrl} target="_blank" rel="noopener noreferrer">
              Start free trial
            </a>
          </Button>
        </div>

        <button
          type="button"
          className="md:hidden p-2 rounded-lg hover:bg-black/5 min-h-[44px]"
          onClick={() => setMobileOpen((o) => !o)}
          aria-label={mobileOpen ? "Close menu" : "Open menu"}
          aria-expanded={mobileOpen}
        >
          {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </div>

      {mobileOpen && (
        <div className="md:hidden border-t border-border bg-card px-6 py-4 shadow-card">
          <nav className="flex flex-col gap-4" aria-label="Mobile navigation">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-sm font-medium text-text-primary hover:text-accent-blue"
                onClick={() => setMobileOpen(false)}
              >
                {link.label}
              </Link>
            ))}
            <div className="flex flex-col gap-2 pt-2">
              <Button variant="outline" size="sm" asChild className="w-full">
                <a href={site.appUrl} target="_blank" rel="noopener noreferrer">
                  Sign in
                </a>
              </Button>
              <Button variant="accent" size="sm" asChild className="w-full">
                <a href={site.appSignupUrl} target="_blank" rel="noopener noreferrer">
                  Start free trial
                </a>
              </Button>
            </div>
          </nav>
        </div>
      )}
    </header>
  );
}
