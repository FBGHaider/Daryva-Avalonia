# Daryva Marketing Site

Next.js 14 (App Router) marketing website for [Daryva](https://daryva.com) — modern property OS for UK landlords and small agencies.

**New to how this was built?** See **[docs/HOW_THIS_SITE_WAS_BUILT.md](docs/HOW_THIS_SITE_WAS_BUILT.md)** for a full step-by-step explanation (project setup, design system, pages, components, static export, and deployment).

## Run locally

```bash
cd web
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Build

```bash
npm run build
```

The site is built as a **static export** (output in `out/`), so it can be deployed to Cloudflare Pages, Vercel, or any static host. To preview locally after building, use e.g. `npx serve out` or open the `out` folder with a static server.

## Where to change copy and theme

- **Site name, URLs, support email, social links:** `lib/site.ts`
- **Global metadata (title template, description, OG):** `app/layout.tsx`
- **Page-specific copy and metadata:** each `app/(marketing)/**/page.tsx` (and `layout.tsx` where used)
- **Design tokens (colors, fonts, radius, shadows):** `tailwind.config.ts` and `app/globals.css`
- **Component styles:** `components/ui/*` and `components/marketing/*`
- **Navigation links:** `components/marketing/header.tsx` and `components/marketing/footer.tsx`

## Using your domain (daryva.com)

### Option A: Deploy to Cloudflare Pages (free, works with GitHub organisations)

You can host the site entirely on Cloudflare Pages. The free plan supports **GitHub organisation repos** (no paid plan required, unlike Vercel Pro for orgs).

1. **Connect the repo**
   - Go to [dash.cloudflare.com](https://dash.cloudflare.com) → **Workers & Pages** → **Create** → **Pages** → **Connect to Git**.
   - Choose **GitHub** and authorise. Select the **FBG-Engineering/Daryva-Avalonia** (or your org) repo.

2. **Configure the build** (use one of the two options below).

   **Option 2a – Root directory set (often fails with “output directory not found”):**
   - **Root directory:** `web`
   - **Build command:** `npm run build`
   - **Build output directory:** `out`

   **Option 2b – Build from repo root, output at repo root (use this if 2a fails):**
   - **Root directory:** leave **empty**.
   - **Build command:** `cd web && npm install && npm run build && cp -r out ../out`
   - **Build output directory:** `out`
   - This builds in `web/`, then copies `web/out` to repo root `out/` so Cloudflare finds the output. Cloudflare’s build runs on Linux, so `cp -r` works.

3. **Deploy**
   - Save and deploy. You’ll get a URL like `your-project.pages.dev`.

4. **Custom domain (daryva.com)**
   - In the Pages project → **Custom domains** → **Set up a custom domain** → enter `daryva.com` (and add `www.daryva.com` if you want).
   - If the domain is already on Cloudflare (DNS managed by Cloudflare), the records are added automatically and SSL is free.
   - If the domain is elsewhere, add the CNAME (or A/AAAA) record that Cloudflare shows.

After deployment, open **https://daryva.com** to view the site.

### Option B: Deploy to Vercel (Pro required for GitHub orgs)

1. **Deploy the site**
   - Go to [vercel.com](https://vercel.com) and sign in (GitHub/GitLab/Bitbucket).
   - **Add New Project** → Import your **Daryva-Avalonia** repo.
   - Set **Root Directory** to `web` (click Edit, then enter `web`).
   - Leave Framework Preset as **Next.js** and Build Command as `npm run build`.
   - Deploy. You’ll get a URL like `your-project.vercel.app`.

2. **Add your domain**
   - In the project, open **Settings → Domains**.
   - Add **daryva.com** and **www.daryva.com**.
   - Vercel will show the DNS records you need.

3. **Configure DNS at your registrar**
   - Log in where you bought daryva.com (e.g. Namecheap, GoDaddy, Cloudflare, Google Domains).
   - For the **root** domain `daryva.com`:
     - Add an **A** record: name `@`, value **76.76.21.21** (Vercel’s IP), or
     - If your registrar supports it, add a **CNAME** for `@` to **cname.vercel-dns.com** (only some registrars allow CNAME on root).
   - For **www**:
     - Add a **CNAME** record: name `www`, value **cname.vercel-dns.com**.
   - Save. DNS can take from a few minutes up to 48 hours (often 5–15 minutes).

4. **SSL**
   - Vercel will issue a free SSL certificate for daryva.com and www once DNS is correct. No extra steps.

5. **Redirect www to root (optional)**
   - In Vercel → Domains, you can set daryva.com as primary and redirect www to it (or the other way around).

After DNS has propagated, open **https://daryva.com** to view the site. Check **https://daryva.com/sitemap.xml** and **https://daryva.com/robots.txt** to confirm.

#### Using Cloudflare for DNS (domain on Cloudflare, site on Vercel)

Yes, this works. If daryva.com uses Cloudflare for DNS:

1. Deploy the site on **Vercel** as above and add **daryva.com** and **www.daryva.com** in Vercel → Settings → Domains.
2. In **Cloudflare Dashboard** → your domain → **DNS** → **Records**:
   - **Root (daryva.com):** Add an **A** record: name `@`, IPv4 address **76.76.21.21**. Turn the cloud **orange (Proxied)** if you want Cloudflare CDN and DDoS protection, or **grey (DNS only)** to point straight to Vercel.
   - **www:** Add a **CNAME** record: name `www`, target **cname.vercel-dns.com**. Orange or grey same as above.
3. **SSL:** In Cloudflare → **SSL/TLS**, use **Full** (or **Full (strict)**) so HTTPS works correctly with Vercel.
4. Vercel will still issue its own certificate; with Cloudflare in front, visitors get HTTPS and you can use Cloudflare caching and security.

Result: daryva.com works with Cloudflare in front of Vercel.

### Option C: Other hosts (Netlify, your own server, etc.)

- **Netlify:** Connect the repo, set base directory to `web`, build command `npm run build`, publish directory `.next` and use Netlify’s Next.js runtime (or run `next build && next start` in a Docker/Node server). Then add daryva.com in Netlify **Domain settings** and follow their DNS instructions.
- **VPS/own server:** Build with `npm run build`, run `npm start` (or use a process manager like PM2), put Nginx/Caddy in front, and point your domain’s A record to the server IP. Configure SSL (e.g. Let’s Encrypt).

### Testing the domain locally (optional)

To open the site at **http://daryva.com** on your machine (e.g. to test links or cookies):

1. Edit your hosts file:
   - **Windows:** `C:\Windows\System32\drivers\etc\hosts` (edit as Administrator).
   - **Mac/Linux:** `/etc/hosts`.
2. Add a line: `127.0.0.1 daryva.com`
3. Run `npm run dev` in the `web` folder and visit **http://daryva.com:3000** (or run the dev server on port 80 if you have permission).

Remove the hosts line when you’re done so daryva.com resolves to the real server again.
