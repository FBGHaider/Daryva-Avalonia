# How This Marketing Site Was Built — Step by Step

This guide explains how the Daryva marketing website (daryva.com) was created from scratch so you can understand the process and reuse the approach for other projects.

---

## 1. What We Built (Overview)

- A **marketing website** for Daryva: multiple pages (Home, Product, Pricing, Demo, Security, About, Contact, Legal).
- **Static site**: no database or server logic; all content is fixed at build time.
- **Deployed** as plain HTML/CSS/JS to Cloudflare Pages (or any static host).
- **Tech**: Next.js 14 (React), TypeScript, Tailwind CSS, and small UI primitives (button, card, etc.) inspired by shadcn/ui.

---

## 2. Why This Stack?

| Choice | Reason |
|--------|--------|
| **Next.js** | React framework with file-based routing, built-in optimisations, and simple static export. |
| **App Router** | Next.js 14’s default: folders under `app/` define routes; `page.tsx` = the page, `layout.tsx` = shared shell. |
| **TypeScript** | Fewer bugs and better editor support. |
| **Tailwind CSS** | Utility classes (e.g. `text-primary`, `rounded-2xl`) so we don’t write custom CSS for every component. |
| **Static export** | `next build` produces a folder of HTML/JS/CSS that any host (Cloudflare, Vercel, Netlify) can serve; no Node server needed. |

---

## 3. Project Structure (Where Everything Lives)

```
web/
├── app/                    # Routes and global layout
│   ├── layout.tsx          # Root layout (fonts, metadata, <html>/<body>)
│   ├── globals.css         # Global styles and Tailwind
│   ├── sitemap.ts          # Generates /sitemap.xml
│   ├── robots.ts           # Generates /robots.txt
│   └── (marketing)/        # Route group: doesn’t change the URL
│       ├── layout.tsx     # Shared header + footer for all marketing pages
│       ├── page.tsx       # Home page → /
│       ├── product/       # → /product
│       ├── pricing/       # → /pricing
│       ├── demo/          # → /demo
│       ├── security/      # → /security
│       ├── about/         # → /about
│       ├── contact/       # → /contact
│       └── legal/
│           ├── privacy/   # → /legal/privacy
│           └── terms/     # → /legal/terms
├── components/
│   ├── ui/                 # Reusable primitives (Button, Card, Input, etc.)
│   └── marketing/          # Sections used on marketing pages (Hero, Footer, etc.)
├── lib/
│   ├── site.ts             # Site constants (name, URLs, support email)
│   ├── brand.ts            # Brand asset paths and titles
│   └── utils.ts            # Small helpers (e.g. cn() for class names)
├── public/                 # Static files (images, favicon, brand assets)
│   ├── brand/
│   └── images/
├── next.config.js          # Next.js config (static export, images)
├── tailwind.config.ts      # Tailwind theme (colors, fonts)
├── package.json
└── tsconfig.json
```

**Important idea:** In the App Router, a **folder** = a segment of the URL. A file named **`page.tsx`** in that folder is the page component. A **`layout.tsx`** wraps all pages under that folder (and is shared).

---

## 4. Step-by-Step: How Each Part Was Built

### Step 4.1 — Create the project

1. **New Node/Next project**  
   We created a `web/` folder and added:
   - `package.json` (with Next.js, React, Tailwind, TypeScript, and a few UI libraries).
   - `next.config.js`, `tailwind.config.ts`, `tsconfig.json`, `postcss.config.js`.

2. **Install dependencies**  
   Run `npm install` in `web/`. That pulls in Next, React, Tailwind, etc.

### Step 4.2 — Configure the design system (Tailwind + CSS)

1. **Tailwind theme** (`tailwind.config.ts`)  
   We defined:
   - **Colors**: e.g. `primary` (#0B1220), `accent` (#21C58E), `background` (#F7F8FA), `text-primary`, `text-muted`, `border`.
   - **Fonts**: heading (Sora / Space Grotesk), body (Inter).
   - **Border radius and shadows** for cards and buttons.

2. **Global CSS** (`app/globals.css`)  
   - Imports Tailwind (`@tailwind base/components/utilities`).
   - Optionally sets CSS variables that match the theme.
   - Applies base styles (e.g. body background and font).

3. **Fonts**  
   In `app/layout.tsx` we use Next.js’s `next/font/google` to load Inter, Sora, and Space Grotesk and attach them to the `<html>` element via class names. Tailwind then uses those via `font-body` and `font-heading`.

### Step 4.3 — Site constants and metadata

1. **`lib/site.ts`**  
   One place for:
   - Site name, tagline, description.
   - URLs (main site, app, signup).
   - Support email, social links.  
   So changing the brand or environment is done in one file.

2. **`lib/brand.ts`**  
   Paths to brand assets (logo, favicon, OG image) and default title/template for the site.

3. **Root layout** (`app/layout.tsx`)  
   - Wraps the whole app (fonts, `<html>`, `<body>`).
   - Exports **metadata** (title template, description, Open Graph, Twitter, icons).  
   Next uses this for `<title>`, meta tags, and link tags so the site is SEO- and share-friendly.

### Step 4.4 — Reusable UI components

We added small, generic components in `components/ui/`:

- **Button** — variants: primary (navy), accent (emerald), outline, ghost; sizes: sm, default, lg.
- **Card** — container with header, title, description, content, footer.
- **Input, Textarea** — styled form fields.
- **Badge** — small labels (e.g. “Most popular”).
- **Accordion** — expand/collapse (e.g. FAQ).
- **Tabs, Switch** — for things like monthly/annual pricing.

Each uses Tailwind classes and, where useful, a small “variant” helper (e.g. `class-variance-authority`) so we can do `<Button variant="accent">` instead of writing long class strings everywhere.

### Step 4.5 — Marketing layout (header + footer)

1. **`app/(marketing)/layout.tsx`**  
   Renders:
   - A **Header** (logo, nav links, “Sign in” / “Start free trial”).
   - The page content (`{children}`).
   - A **Footer** (logo, tagline, links, copyright).

2. **Header** (`components/marketing/header.tsx`)  
   - Uses the logo (icon + “Daryva”) and links to Product, Pricing, Demo, Security, About, Contact.
   - “Sign in” and “Start free trial” point to your app URLs (from `lib/site.ts`).
   - On scroll, the header can get a slight background/blur (sticky, opaque).
   - Mobile: hamburger menu that shows the same links and CTAs.

3. **Footer** (`components/marketing/footer.tsx`)  
   - Same logo + short tagline.
   - Columns: Product links, Company links, Legal (Privacy, Terms).
   - Copyright line.

All marketing pages automatically get this shell because they sit inside `(marketing)/` and use that layout.

### Step 4.6 — Home page (and sections)

1. **`app/(marketing)/page.tsx`**  
   This is the **Home** page (`/`). It doesn’t render much itself; it composes **sections**:

   - **Hero** — headline, subheadline, two buttons (Start free trial, Book a demo), trust line, and a dashboard screenshot (image in a browser-style frame).
   - **Trust row** — short trust bullets (e.g. “Built for UK landlords”, “Secure multi-user”).
   - **Problem / Solution** — “Before Daryva” vs “After Daryva” in two columns.
   - **Feature highlights** — e.g. three cards: Rent & payments, Document vault, Notifications.
   - **How it works** — e.g. three steps (Add properties, Invite, Track).
   - **Screenshot gallery** — grid of placeholders or images for Dashboard, Tenants, Payments, etc.
   - **Pricing preview** — three tiers (Starter, Pro, Agency) with an annual toggle.
   - **FAQ** — accordion of questions and answers.
   - **Final CTA** — band with “Start your free trial” and “Book a demo”.

2. **Section components**  
   Each section is its own component under `components/marketing/` (e.g. `hero.tsx`, `trust-row.tsx`, `pricing-preview.tsx`, `faq.tsx`). The home page imports them and places them one after another. That keeps the home page simple and makes sections reusable or easy to reorder.

### Step 4.7 — Other pages

For each route we added a folder and a `page.tsx`:

- **Product** (`app/(marketing)/product/page.tsx`) — alternating blocks of copy and screenshot placeholders for features.
- **Pricing** (`app/(marketing)/pricing/page.tsx` + `layout.tsx`) — full pricing cards and a small FAQ; layout can export pricing-specific metadata.
- **Demo** — video placeholder + “Book a demo” form + link to free trial.
- **Security** — short text about HTTPS, auth, data isolation, backups, GDPR.
- **About** — mission, “Built in Cambridge”, values.
- **Contact** — contact form (name, email, message) and support email.
- **Legal** — `legal/privacy/page.tsx` and `legal/terms/page.tsx` with placeholder policy and terms.

Each `page.tsx` exports optional **metadata** (title, description) so the root layout’s title template becomes e.g. “Product | Daryva”. Forms (contact, demo) are client components that validate and, for now, log or stub submit; you can later wire them to an API or email service.

### Step 4.8 — Images and assets

1. **`public/`**  
   Files here are served at the root: `public/brand/logo.png` → `/brand/logo.png`.

2. **Logo and favicon**  
   We put the app icon in `public/brand/` (e.g. `Daryva_icon.ico`, `favicon.ico`) and reference it in the header/footer and in layout metadata (icons, OG image).

3. **Dashboard screenshot**  
   The hero uses a real dashboard image: e.g. `public/images/dashboard-screenshot.png`. A small component wraps it in a browser-style frame and uses `next/image` for responsive loading.

4. **Next.js config**  
   For static export we set `images: { unoptimized: true }` so images work without a server.

### Step 4.9 — SEO and static files

1. **Sitemap**  
   `app/sitemap.ts` returns a list of URLs (/, /product, /pricing, …). Next turns this into `/sitemap.xml` at build time.

2. **Robots**  
   `app/robots.ts` returns rules and the sitemap URL. Next generates `/robots.txt`.

3. **Metadata**  
   Every page can set `title`, `description`, and Open Graph/Twitter fields. The root layout sets the default and template (e.g. “%s | Daryva”).

### Step 4.10 — Static export and build

1. **`next.config.js`**  
   We set `output: "export"`. So `next build` produces a static site in the `out/` directory (no server-side rendering at runtime).

2. **Build**  
   Running `npm run build` in `web/`:
   - Compiles TypeScript and React.
   - Pre-renders every page to HTML.
   - Generates JS/CSS chunks and writes everything into `out/`.
   - Puts `sitemap.xml` and `robots.txt` in `out/` as well.

3. **Preview**  
   You can serve the built site locally with e.g. `npx serve out` to simulate production.

### Step 4.11 — Deployment (Cloudflare Pages)

1. **Connect repo**  
   In Cloudflare Pages we connect the GitHub repo (e.g. fbg-engineering/Daryva-Avalonia).

2. **Build settings**  
   Because the Next app lives in `web/`:
   - **Root directory**: left empty (build from repo root).
   - **Build command**: `cd web && npm install && npm run build && cp -r out ../out`.
   - **Build output directory**: `out`.

   This runs the build inside `web/`, then copies `web/out` to the repo root `out/` so Cloudflare finds the static files at `out/`.

3. **Custom domain**  
   In Pages we add daryva.com (and optionally www). If the domain is on Cloudflare, DNS is set up for you; otherwise we add the CNAME (or A/AAAA) they show.

4. **Result**  
   Every push to the main branch can trigger a new build; the live site is served from Cloudflare’s CDN with HTTPS.

---

## 5. Concepts Worth Remembering

- **Route = folder + `page.tsx`**  
  `app/(marketing)/pricing/page.tsx` → URL `/pricing`. The `(marketing)` part is a “route group”: it doesn’t appear in the URL but lets us share a layout.

- **Layouts wrap pages**  
  `app/layout.tsx` wraps the whole app. `app/(marketing)/layout.tsx` wraps all marketing pages with the same header and footer.

- **Client vs server**  
  By default, components are “server” (run on the server during build). If a component needs state or browser APIs (e.g. a form, a toggle), we put `"use client"` at the top so it runs in the browser.

- **Static export**  
  With `output: "export"`, there is no Node server in production. The host only serves the files in `out/`. That’s why we use `unoptimized` images and no server-only features (e.g. no API routes that run at request time).

- **One source of truth**  
  Site name, URLs, and support email live in `lib/site.ts` and `lib/brand.ts`. Changing them there updates the whole site (header, footer, metadata, links).

---

## 6. Quick Reference: “I want to…”

| Goal | Where to look |
|------|----------------|
| Change site name or app URL | `lib/site.ts`, `lib/brand.ts` |
| Change colours or fonts | `tailwind.config.ts`, `app/globals.css` |
| Edit home page sections | `app/(marketing)/page.tsx` and `components/marketing/*` |
| Add or change a page | Add a folder under `app/(marketing)/` with a `page.tsx` |
| Change header or footer links | `components/marketing/header.tsx`, `footer.tsx` |
| Change SEO title/description | `app/layout.tsx` (default) or the specific page’s `metadata` export |
| Replace the dashboard screenshot | Replace `public/images/dashboard-screenshot.png` |
| Change deploy settings | Cloudflare Pages project → Build configuration; see `README.md` in `web/` |

---

## 7. Summary

The site was built by:

1. Creating a Next.js 14 app in `web/` with TypeScript and Tailwind.
2. Defining a design system in Tailwind and global CSS.
3. Putting shared data in `lib/site.ts` and `lib/brand.ts`.
4. Building small UI components and a marketing layout (header + footer).
5. Implementing the home page as a composition of section components, and adding one page per route under `app/(marketing)/`.
6. Adding images and brand assets under `public/`, plus sitemap and robots.
7. Enabling static export and building to `out/`.
8. Deploying `out/` to Cloudflare Pages with a custom domain.

You can reuse this flow for any similar static marketing or landing site: same stack, same folder structure, and the same “sections + layout + static export” idea.
