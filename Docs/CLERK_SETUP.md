# Clerk setup: Web app, Desktop app, and API

You have your Clerk API keys. Follow these steps so sign-in works across the **web app** (app.daryva.com), the **desktop app**, and the **Daryva API**.

---

## 1. Web app (Next.js in `web/`)

**Goal:** Sign-in and sign-up pages at app.daryva.com use Clerk.

1. In `web/` copy the example env file:
   ```bash
   cp .env.local.example .env.local
   ```
2. Edit `web/.env.local` and set:
   - `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` = your **Publishable key** (starts with `pk_test_` or `pk_live_`)
   - `CLERK_SECRET_KEY` = your **Secret key** (starts with `sk_test_` or `sk_live_`)
3. In **Clerk Dashboard** → **Configure** → **Domains**: add your app domain(s), e.g. `localhost:3000` for dev and `app.daryva.com` for production.
4. Run the web app: `cd web && npm run dev`. Open http://localhost:3000/sign-in — you should see the Clerk sign-in form.

---

## 2. Clerk Dashboard: OAuth application for the desktop app

**Goal:** Let the desktop app use Clerk via OIDC (browser opens → user signs in → redirect back to app).

**Important:** The desktop app’s **Oidc:ClientId** must be the **Client ID of an OAuth Application** you create in Clerk for the desktop app. Do **not** use your Publishable Key (`pk_...`), Secret Key (`sk_...`), or any other ID — only the Client ID from **OAuth Applications**.

1. In **Clerk Dashboard** go to **Configure** → **OAuth Applications** (direct link: [dashboard.clerk.com/~/oauth-applications](https://dashboard.clerk.com/~/oauth-applications)).
2. Click **Add OAuth application** and create one for the Daryva desktop app (e.g. name: “Daryva Desktop”).
3. In **Redirect URIs** add exactly:
   ```text
   http://127.0.0.1:58432/callback
   ```
   (The desktop app listens on port 58432 for the OIDC callback.)
4. Save and copy the **Client ID** shown for this OAuth application. Use this value as **Oidc:ClientId** in the desktop config.
5. Get your **Clerk Issuer (Authority) URL**:
   - Often found under **Configure** → **Domains** or **API Keys** as the “Frontend API” or “Issuer” (e.g. `https://xxx.clerk.accounts.dev` or your custom domain).
   - Or open: `https://<your-clerk-domain>/.well-known/openid-configuration` and use the `issuer` value from the JSON.

---

## 3. Desktop app (Avalonia) config

**Goal:** “Sign in” in the desktop app opens the browser to Clerk and receives the token.

1. Find or create the desktop app’s local config file (e.g. `app.config.local.json` in the app data folder or next to the executable — see `src/Daryva.UI/app.config.local.example.json`).
2. Set in `AppSettings`:
   - **Oidc:Authority** = your Clerk issuer URL (e.g. `https://xxx.clerk.accounts.dev` — no trailing slash, or with `/`; the app normalises it).
   - **Oidc:ClientId** = the **Client ID** of the OAuth application you created for the desktop app in step 2.
   - **ApiBaseUrl** = your API base URL (see below for dev vs prod).
   - **AppOnboardingUrl** = `https://app.daryva.com/onboarding` (or your app URL).

**Optional — Application configuration URLs:** In Clerk, your OAuth application’s “Application configuration URLs” panel lists the same endpoints the app uses via discovery. You can copy those URLs into your local config for reference (they must match your **Oidc:Authority** base). Optional keys: **Oidc:DiscoveryUrl**, **Oidc:AuthorizeUrl**, **Oidc:TokenUrl**, **Oidc:UserInfoUrl**. The app uses **Oidc:Authority** for OIDC discovery, which returns these same endpoints.

Example:

```json
{
  "AppSettings": {
    "ApiBaseUrl": "http://localhost:5000",
    "Oidc:Authority": "https://xxx.clerk.accounts.dev",
    "Oidc:ClientId": "your-desktop-oauth-client-id",
    "AppOnboardingUrl": "https://app.daryva.com/onboarding",
    "Oidc:DiscoveryUrl": "https://xxx.clerk.accounts.dev/.well-known/openid-configuration",
    "Oidc:AuthorizeUrl": "https://xxx.clerk.accounts.dev/oauth/authorize",
    "Oidc:TokenUrl": "https://xxx.clerk.accounts.dev/oauth/token",
    "Oidc:UserInfoUrl": "https://xxx.clerk.accounts.dev/oauth/userinfo"
  }
}
```

**ApiBaseUrl by environment:**
- **Local dev:** `http://localhost:5000` (API runs on port 5000 in Visual Studio / `dotnet run`).
- **Production (server by IP):** Use port **8080**, not 5000 — the API in Docker listens on 8080. Example: `http://YOUR_SERVER_IP:8080` (e.g. `http://46.225.87.78:8080`). Ensure the server firewall allows inbound TCP 8080.
- **Production (domain with Cloudflare):** Use `https://api.daryva.com` only if you have a reverse proxy (e.g. nginx/caddy) on the server listening on 443 and forwarding to the API on port 8080. Cloudflare proxy connects to your origin on 443 by default; port 5000 is not used in production.

3. Run the desktop app and click **Sign in** — the browser should open to Clerk (or your sign-in page); after signing in you should be redirected back and the app should receive the token.

---

## 4. API (Daryva.Api) config

**Goal:** The API accepts and validates JWTs issued by Clerk.

1. Edit `src/Daryva.Api/appsettings.Development.json` (and production config when you deploy):
2. Under **Jwt** set:
   - **Authority** = same Clerk issuer URL as in the desktop app (e.g. `https://xxx.clerk.accounts.dev`).
   - **Audience** = the audience your Clerk JWTs use (often your Clerk application identifier or a custom value; check Clerk JWT template or docs). If Clerk does not set a specific audience, you may need to relax or adjust audience validation in code.
3. Leave **SigningKey** empty when using **Authority** (the API will use Clerk’s OIDC discovery and signing keys).
4. Restart the API. It should now validate Bearer tokens issued by Clerk.

Example (development):

```json
"Jwt": {
  "Authority": "https://xxx.clerk.accounts.dev",
  "Audience": "daryva-api",
  "Issuer": "daryva-api",
  "SigningKey": "",
  ...
}
```

If your Clerk JWTs use a different audience, set **Audience** to that value. If the API still rejects tokens, check Clerk’s JWT template and claims (e.g. `aud`, `iss`).

---

## 5. Optional: Use app.daryva.com as the sign-in page

By default the desktop app sends the user to the **OIDC authorize URL** (on the Clerk domain). If you want the user to land on **app.daryva.com/sign-in** (your Next.js page with Clerk’s `<SignIn />` component):

- In Clerk Dashboard, check **Paths** or **URLs** (e.g. **Configure** → **Paths**) and set the **Sign-in URL** to `https://app.daryva.com/sign-in` (and sign-up to `https://app.daryva.com/sign-up`) if your plan supports custom hostnames.
- Or use Clerk’s hosted UI and configure the redirect so that after login the user is sent back to the desktop’s loopback URL; the exact steps depend on your Clerk plan and how you register the OAuth app.

---

## Quick checklist

- [ ] Web: `web/.env.local` has `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` and `CLERK_SECRET_KEY`.
- [ ] Clerk: OAuth application created for desktop app with redirect URI `http://127.0.0.1:58432/callback`.
- [ ] Clerk: Issuer (Authority) URL noted.
- [ ] Desktop: `app.config.local.json` has `Oidc:Authority` and `Oidc:ClientId` (and `ApiBaseUrl`, `AppOnboardingUrl`).
- [ ] API: `Jwt:Authority` set to Clerk issuer; `Jwt:Audience` matches Clerk JWTs if required.
- [ ] Test: Web sign-in at /sign-in, then desktop “Sign in”, then call API with the token.

---

## Troubleshooting

### "invalid_client" / "The requested OAuth 2.0 Client does not exist"

This means the **Oidc:ClientId** in your desktop config is not recognized by Clerk.

- **Cause:** You are not using the Client ID of an **OAuth Application**. For example, the Publishable Key (`pk_test_...`) or another dashboard ID will not work.
- **Fix:**
  1. Go to [Clerk Dashboard → OAuth Applications](https://dashboard.clerk.com/~/oauth-applications).
  2. Create a new OAuth application (e.g. “Daryva Desktop”) and add redirect URI `http://127.0.0.1:58432/callback`.
  3. Copy that application’s **Client ID** (it is not the same as your Publishable or Secret key).
  4. Put that Client ID in `app.config.local.json` as **Oidc:ClientId** and restart the desktop app.
  5. **Public = ON:** In the OAuth app settings, ensure **Public** is turned **ON** (required for PKCE / no client secret).
  6. **Same instance:** Confirm **Oidc:Authority** matches your Clerk instance (e.g. `https://merry-marmoset-71.clerk.accounts.dev`). If it still fails, create a **new** OAuth application and use its Client ID.

### 401 Unauthorized when creating an organisation (desktop)

The desktop shows "Signed in with your@email.com" but creating an organisation returns **401 (Unauthorized)**.

- **Cause:** The API is not validating your Clerk token. In development this usually means **Jwt:Authority** is empty in `src/Daryva.Api/appsettings.Development.json`, so the API uses a local signing key and rejects Clerk-issued JWTs.
- **Fix:** In `appsettings.Development.json` set **Jwt:Authority** to your Clerk issuer URL (same as the desktop’s **Oidc:Authority**), e.g. `https://xxx.clerk.accounts.dev`. Leave **SigningKey** unused when using Authority. Restart the API. See [section 4. API config](#4-api-daryvaapi-config) above.

### "invalid_scope" / "The OAuth 2.0 Client is not allowed to request scope 'openid'"

Clerk is rejecting the requested scopes because the OAuth application does not have them enabled.

- **Fix:** In [Clerk Dashboard → OAuth Applications](https://dashboard.clerk.com/~/oauth-applications), open your desktop OAuth app. Under **Scopes**, enable **openid**, **profile**, **email**, and **offline_access**. The **openid** scope is required for OIDC; save and try sign-in again.
