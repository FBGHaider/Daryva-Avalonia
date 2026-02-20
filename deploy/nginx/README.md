# Nginx setup for daryva.com + api.daryva.com

This config is for a VPS where:
- API Docker container is reachable at `127.0.0.1:8080`
- Website is hosted from `/var/www/daryva.com`

## 1) Install Nginx + Certbot

```bash
sudo apt update
sudo apt install -y nginx certbot python3-certbot-nginx
```

## 2) Put config in place

Copy `deploy/nginx/daryva.conf` to:
- `/etc/nginx/sites-available/daryva.conf`

Then enable:

```bash
sudo ln -sf /etc/nginx/sites-available/daryva.conf /etc/nginx/sites-enabled/daryva.conf
sudo nginx -t
sudo systemctl reload nginx
```

## 3) Issue TLS certificate

```bash
sudo certbot --nginx -d daryva.com -d www.daryva.com -d api.daryva.com
```

## 4) API container exposure

The API service should publish host port `8080`:

```yaml
ports:
  - "8080:8080"
```

## 5) Validate

```bash
curl -I https://daryva.com
curl -I https://api.daryva.com/health
```

Expected:
- Site responds on HTTPS
- API health responds with `200 OK`
