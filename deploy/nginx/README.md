# Nginx setup for daryva.com + api.daryva.com + portal.daryva.com

The VPS runs **three independent Nginx sites**, each with its own
Certbot-managed certificate:

- `daryva-api.conf` → `api.daryva.com`, proxies to the API Docker container on
  `127.0.0.1:8080`. Cert lineage: `/etc/letsencrypt/live/api.daryva.com/`.
- `daryva-website.conf` → `daryva.com` + `www.daryva.com`, serves static files
  from `/var/www/daryva.com`. Cert lineage: `/etc/letsencrypt/live/daryva.com/`.
- `daryva-portal.conf` → `portal.daryva.com`, proxies to the portal's Docker
  container on `127.0.0.1:8081`. Cert lineage:
  `/etc/letsencrypt/live/portal.daryva.com/`.

Keeping them separate means a deploy or cert renewal for one can never
accidentally break the others.

## 1) Install Nginx + Certbot

```bash
sudo apt update
sudo apt install -y nginx certbot python3-certbot-nginx
```

## 2) Put configs in place

Copy each file to `/etc/nginx/sites-available/` under the name Certbot expects
(no `.conf` suffix, matching what's live on the VPS today):

```bash
sudo cp deploy/nginx/daryva-api.conf /etc/nginx/sites-available/daryva-api
sudo cp deploy/nginx/daryva-website.conf /etc/nginx/sites-available/daryva-website
sudo cp deploy/nginx/daryva-portal.conf /etc/nginx/sites-available/daryva-portal
sudo ln -sf /etc/nginx/sites-available/daryva-api /etc/nginx/sites-enabled/daryva-api
sudo ln -sf /etc/nginx/sites-available/daryva-website /etc/nginx/sites-enabled/daryva-website
sudo ln -sf /etc/nginx/sites-available/daryva-portal /etc/nginx/sites-enabled/daryva-portal
sudo nginx -t
sudo systemctl reload nginx
```

The committed files here already include the `# managed by Certbot` blocks
exactly as they exist on the server, since Certbot rewrites the file in place
when it issues/renews a cert. If you're setting this up fresh, start with just
the `server_name`/`root`/`location` parts on port 80, run Certbot (step 3), and
let it add the SSL blocks itself — that's how all three were originally created.

## 3) Issue TLS certificates

Three separate certs, issued independently:

```bash
sudo certbot --nginx -d api.daryva.com
sudo certbot --nginx -d daryva.com -d www.daryva.com --redirect
sudo certbot --nginx -d portal.daryva.com --redirect
```

Certbot registers one Let's Encrypt account per server on first use and reuses
it for later certs, so the second and third commands won't re-prompt for an
email/ToS once the first has run.

## 4) Container exposure

The API and portal services should publish their own host ports:

```yaml
# api
ports:
  - "8080:8080"
# portal
ports:
  - "8081:8080"
```

## 5) Validate

```bash
curl -I https://daryva.com
curl -I https://www.daryva.com
curl -I https://api.daryva.com/health
curl -I https://portal.daryva.com
```

Expected: all four respond over HTTPS, and `api.daryva.com/health` returns
`200 OK`.

**Don't stop at the status code** — a `200` can come from the wrong place (a
stale DNS target, a misrouted Nginx `server_name` fallback, a proxy hitting
the API instead of the static root). Check the actual content too:

```bash
curl -s https://daryva.com | grep -o '<title>[^<]*</title>'
```

## Known pitfall (already hit once)

If `daryva.com`/`www.daryva.com` return a `200` that doesn't match the actual
site content, or an empty `404` body, it usually means no Nginx `server_name`
block actually matches those hostnames — requests fall through to whichever
`443` server block Nginx picks by default (often `daryva-api`, whose proxy
target then returns its own 404 for the unmatched route). Confirm with:

```bash
sudo nginx -T | grep -B2 -A20 "server_name daryva.com"
```

If that returns nothing, `daryva-website` isn't actually enabled — check
`/etc/nginx/sites-enabled/`.
