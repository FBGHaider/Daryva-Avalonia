# Daryva Production Deployment (daryva.com)

This runbook sets up:
- Website on `https://daryva.com`
- API on `https://api.daryva.com`
- Always-on API with PostgreSQL + SMTP verification email

## 1) DNS

At your DNS provider, create:
- `A` record: `@` -> web host IP (or CNAME if your web host supports apex alias)
- `CNAME` record: `www` -> `daryva.com`
- `A` or `CNAME` record: `api` -> API host endpoint

## 2) Email domain authentication

Configure your email provider for `daryva.com` and add DNS records they provide:
- SPF (TXT)
- DKIM (TXT/CNAME)
- DMARC (TXT)

Recommended sender:
- `no-reply@daryva.com`

## 3) API deployment (Docker)

Use `docker-compose.prod.yml` as a base. Before deploying, replace all `change_me` values.

Use `.env.prod.example` as your server template:
- copy to `.env` on the VPS
- set real secrets there
- never commit `.env`

Run on your server:

```powershell
docker compose -f docker-compose.prod.yml up -d
```

## 4) Required API environment values

Set these (from compose or hosting env vars):
- `ConnectionStrings__DefaultConnection`
- `Jwt__SigningKey` (32+ chars)
- `Auth__AllowAnyLogin=false`
- `Auth__VerificationUrlBase=https://api.daryva.com/api/auth/verify-email`
- `Cors__AllowedOrigins__0=https://daryva.com`
- `Cors__AllowedOrigins__1=https://www.daryva.com`
- `Smtp__Server`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`, `Smtp__EnableSsl`, `Smtp__FromAddress`

## 5) Database migration

After deployment, run once:

```powershell
dotnet ef database update --project src/Daryva.Api/Daryva.Api.csproj --startup-project src/Daryva.Api/Daryva.Api.csproj
```

(or run migration in a one-off API container job)

## 6) HTTPS and reverse proxy

Use Nginx/Caddy/Traefik or managed ingress in your host.
- Route `api.daryva.com` -> API container port `8080`
- Enable TLS certificate auto-renewal

Reference Nginx files in this repo:
- `deploy/nginx/daryva.conf`
- `deploy/nginx/README.md`

## 7) Validation checklist

- `https://api.daryva.com/health` returns healthy
- register endpoint returns `verificationEmailSent=true`
- verification email arrives and verify link works
- login works after verification
- no local/dev credentials in production config

## 8) GitHub Actions auto-deploy

Workflow file:
- `.github/workflows/deploy-api.yml`

Launch schedule guide:
- `Docs/launch-this-week.md`

Set these repository secrets in GitHub:
- `DEPLOY_HOST` (VPS hostname/IP)
- `DEPLOY_USER` (SSH user)
- `DEPLOY_SSH_KEY` (private key for deploy user)
- `DEPLOY_PORT` (optional, default `22`)
- `DEPLOY_PATH` (folder on VPS containing `docker-compose.prod.yml`)
- `GHCR_PAT` (PAT with `read:packages` so VPS can pull GHCR images)

Server prerequisites:
- Docker + Docker Compose plugin installed
- `docker-compose.prod.yml` present in `DEPLOY_PATH`
- Firewall open for `80/443` (and `22` for SSH)

Deploy behavior:
- On push to `master` (API/deploy files), workflow builds image and pushes to GHCR (`latest` + commit SHA tags)
- Workflow SSHes into VPS, deploys the commit-SHA image, and restarts only `api` service
