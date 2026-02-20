# VPS Access README

This guide is a quick reference for opening the VPS, logging in from Windows, and common commands you will use for Daryva production.

## 1) Login from Windows (PowerShell)

Use your working key and server IP:

```powershell
ssh -i "$env:USERPROFILE\.ssh\daryva_prod_ed25519_2026_new" root@46.225.87.78
```

Key-only login test (no password fallback):

```powershell
ssh -i "$env:USERPROFILE\.ssh\daryva_prod_ed25519_2026_new" -o PreferredAuthentications=publickey -o PasswordAuthentication=no root@46.225.87.78 "echo SSH_OK"
```

## 2) Where app deploy files live on VPS

```bash
cd /opt/daryva
ls
```

You should see `docker-compose.prod.yml` in this folder.

## 3) Useful deploy commands on VPS

Pull latest API image and restart API service:

```bash
cd /opt/daryva
docker compose -f docker-compose.prod.yml pull api
docker compose -f docker-compose.prod.yml up -d api
```

Check service status:

```bash
docker compose -f docker-compose.prod.yml ps
```

Follow API logs live:

```bash
docker compose -f docker-compose.prod.yml logs -f api
```

Last 200 lines of API logs:

```bash
docker compose -f docker-compose.prod.yml logs --tail=200 api
```

## 4) Health checks

From VPS:

```bash
curl -sS https://api.daryva.com/health
```

From Windows:

```powershell
Invoke-RestMethod https://api.daryva.com/health
```

## 5) Docker cleanup commands

Remove dangling images:

```bash
docker image prune -f
```

Show disk usage by Docker:

```bash
docker system df
```

## 6) SSH troubleshooting quick checks

Check effective SSH options:

```bash
sshd -T | grep -E 'pubkeyauthentication|passwordauthentication|authorizedkeysfile|permitrootlogin'
```

Restart SSH daemon safely:

```bash
sshd -t && (systemctl reload ssh || systemctl reload sshd)
```

Recent auth logs:

```bash
tail -n 100 /var/log/auth.log
```

## 7) GitHub Actions secrets reminder

Required repository secrets for deploy workflow:

- `DEPLOY_HOST=46.225.87.78`
- `DEPLOY_USER=root`
- `DEPLOY_PORT=22`
- `DEPLOY_PATH=/opt/daryva`
- `DEPLOY_SSH_KEY=<private key content from daryva_prod_ed25519_2026_new>`
- `GHCR_USERNAME=<your github username/org>`
- `GHCR_PAT=<token with read:packages>`

Get private key content for `DEPLOY_SSH_KEY` from Windows:

```powershell
Get-Content -Raw "$env:USERPROFILE\.ssh\daryva_prod_ed25519_2026_new"
```

## 8) Quick one-liner checklist

1. SSH login works with key-only test.
2. `cd /opt/daryva` exists.
3. `docker compose ... pull api` succeeds.
4. `docker compose ... up -d api` succeeds.
5. `https://api.daryva.com/health` is healthy.
