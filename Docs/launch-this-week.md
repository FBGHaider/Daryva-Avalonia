# Daryva Launch This Week (Safe + Secure)

Goal this week:
- Website live on `https://daryva.com`
- API live on `https://api.daryva.com`
- Desktop app connected to online API
- Email verification live in production

## Day 0 (Today): Foundation + Accounts

Create/confirm accounts:
- Domain/DNS: Cloudflare (free)
- VPS: Hetzner / Contabo / DigitalOcean
- Email provider: Resend or Brevo
- Uptime monitoring: UptimeRobot (free)

Create this DNS now:
- `A @ -> <website host or VPS IP>`
- `CNAME www -> daryva.com`
- `A api -> <VPS IP>`

## Day 1: Server hardening

On VPS:
- Create non-root deploy user
- Disable password SSH login (key only)
- Enable firewall (open only `22`, `80`, `443`)
- Install Docker + Compose plugin
- Install fail2ban
- Enable unattended security updates

## Day 2: API + DB deploy

In deployment folder on VPS:
- Copy `docker-compose.prod.yml`
- Copy `.env.prod.example` as `.env`
- Fill all real secrets in `.env`

Run:
```bash
docker compose -f docker-compose.prod.yml up -d
```

Verify:
```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs api --tail=200
```

## Day 3: Nginx + TLS

- Apply `deploy/nginx/daryva.conf`
- Issue TLS certs for `daryva.com`, `www.daryva.com`, `api.daryva.com`
- Ensure auto-renewal works

Must pass:
- `https://api.daryva.com/health` returns healthy

## Day 4: Email verification go-live

Configure provider domain records:
- SPF
- DKIM
- DMARC

Set SMTP secrets in server `.env`.

Must pass:
- Register user -> `verificationEmailSent=true`
- Inbox receives verification email
- Verification link successfully verifies account

## Day 5: Desktop app to production API

For production users, set local config:
- `%AppData%/Daryva/app.config.local.json`
- `AppSettings.ApiBaseUrl = "https://api.daryva.com"`

Smoke test:
- Login, create/read entities, notification/email action

## Day 6: CI/CD + rollback

GitHub repo secrets required:
- `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_PATH`
- optional `DEPLOY_PORT`
- `GHCR_PAT` (`read:packages`)

Run deploy workflow manually once.

Rollback plan:
- Keep previous image tag
- If issue: set `DARYVA_API_IMAGE=<previous_sha_tag>` and re-run:
```bash
docker compose -f docker-compose.prod.yml up -d api
```

## Day 7: Launch checks + monitoring

Set UptimeRobot monitors:
- `https://api.daryva.com/health`
- `https://daryva.com`

Add backups:
- Daily Postgres dump + off-server copy

Suggested backup command:
```bash
docker exec daryva-postgres-prod pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" > /opt/backups/daryva-$(date +%F).sql
```

## Mandatory security checklist

- `Auth__AllowAnyLogin=false` in production
- Dev auth not exposed in production
- No secrets in git
- Strong unique passwords for DB/SMTP/JWT
- HTTPS only (no public HTTP)
- CORS restricted to your real domains only
- Least privilege accounts for server/provider access

## Cheapest reliable stack (recommended)

- Cloudflare DNS/WAF/CDN: free
- VPS (Hetzner CX22 / similar): low-cost always-on host
- Resend/Brevo: free tier to start
- UptimeRobot: free

Typical starter monthly cost: low, but not fully free for always-on production.
