# Running EF migrations when the API is on a Hetzner VPS (Docker)

Your API runs in Docker; the API image has no `dotnet ef` tool. Use one of these approaches.

---

## Option 1: From your PC via SSH tunnel (recommended, one-time)

You run the migration on your PC; the database connection is tunneled through SSH so you don’t expose PostgreSQL to the internet.

### 1. On the VPS: expose Postgres only on localhost

SSH into the VPS and edit the compose file:

```bash
ssh root@YOUR_VPS_IP
cd /opt/daryva
nano docker-compose.prod.yml   # or vim
```

Under the **postgres** service, add a `ports` section so it’s only reachable on the host’s loopback (so the tunnel can reach it):

```yaml
  postgres:
    image: postgres:16-alpine
    container_name: daryva-postgres-prod
    ports:
      - "127.0.0.1:5432:5432"   # add this line
    environment:
      # ... rest unchanged
```

Save, then restart so the port is active:

```bash
docker compose -f docker-compose.prod.yml up -d
```

### 2. On your PC: open an SSH tunnel (leave this terminal open)

In a terminal on your **Windows PC**:

```powershell
ssh -L 5432:127.0.0.1:5432 root@YOUR_VPS_IP
```

Leave this session open; it forwards your local `5432` to the VPS’s local Postgres.

### 3. On your PC: run the migration

In **another** terminal on your PC, use the same DB credentials as on the VPS (from `/opt/daryva/.env`: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`). Replace the password and DB name if different:

```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia\src\Daryva.Api"
dotnet ef database update --connection "Host=localhost;Port=5432;Database=daryva;User Id=daryva;Password=YOUR_POSTGRES_PASSWORD"
```

(If your `.env` uses different names, use those for `Database=` and `User Id=`.)

### 4. (Optional) Stop exposing Postgres on the VPS

If you want to close the port again, remove the `ports` block from the **postgres** service and run:

```bash
docker compose -f docker-compose.prod.yml up -d
```

---

## Option 2: Run migrations from the VPS using a migrate container

This only works if the **full repo** is on the VPS (e.g. you `git clone` there to deploy). Your current `/opt/daryva` only has the compose file and `.env`, so use **Option 1** unless you clone the repo.

If you have the repo on the VPS:

```bash
cd /path/to/Daryva-Avalonia   # repo root (must contain src/, docker-compose.prod.yml, etc.)
docker compose -f docker-compose.prod.yml run --rm migrate
```

The `migrate` service (in `docker-compose.prod.yml`) builds an image with the EF tools and runs `dotnet ef database update` using the same `API_CONNECTION_STRING` as the API, so it connects to the `postgres` container on the Docker network.
