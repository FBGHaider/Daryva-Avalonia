# Daryva: Workflow After Making Changes & Deploying

This guide covers what to do after you change anything in the app: local build and run, tests, database migrations, and deploying the API or desktop app.

---

## Quick reference (commands)

| What you did | What to run |
|--------------|-------------|
| Any code change | Build: `dotnet build Daryva-Avalonia.sln` |
| Any code change | Run locally: `Scripts\restart-dev.ps1` or run API + UI manually |
| API or shared code | Tests: `dotnet test Daryva-Avalonia.sln` |
| API / Data (schema) | Migrations: `dotnet ef database update --project src/Daryva.Api --startup-project src/Daryva.Api` |
| Deploy API to production | Push to `master` (API changes only) or run “Deploy API” workflow manually |
| Package desktop installer | `Tools\Velopack-installer\build-win.ps1 -Version 1.2.3` |

---

## 1. After changing any code

### 1.1 Build the solution

From the **repo root** (where `Daryva-Avalonia.sln` is):

```powershell
dotnet build Daryva-Avalonia.sln
```

- **Release:** `dotnet build Daryva-Avalonia.sln -c Release`
- Fix any build errors before running or deploying.

### 1.2 Run locally (API + UI)

**Option A – Script (recommended)**  
Stops existing API/UI, applies migrations, starts API then UI:

```powershell
.\Scripts\restart-dev.ps1
```

- API only: `.\Scripts\restart-dev.ps1 -ApiOnly`
- Skip migrations: `.\Scripts\restart-dev.ps1 -SkipMigrations`
- Show API/UI in separate terminals: `.\Scripts\restart-dev.ps1 -ShowTerminals`

**Option B – Manual**

1. Start PostgreSQL (if using Docker):

   ```powershell
   docker compose up -d
   ```

2. Apply migrations (if you changed schema):

   ```powershell
   dotnet ef database update --project src/Daryva.Api --startup-project src/Daryva.Api
   ```

3. Run API:

   ```powershell
   cd src\Daryva.Api
   dotnet run
   ```

4. In another terminal, run UI:

   ```powershell
   cd src\Daryva.UI
   dotnet run
   ```

- API default: `http://localhost:5000` (or port in `launchSettings.json`).
- Ensure `appsettings.json` / env has the correct DB connection (e.g. local Postgres or SQLite).

---

## 2. Run tests

After changing API or shared logic:

```powershell
dotnet test Daryva-Avalonia.sln
```

- Release: `dotnet test Daryva-Avalonia.sln -c Release`
- CI runs the same on every PR and push to `master`.

---

## 3. Database migrations (when you change API/Data schema)

When you add or change entities, DbContext, or migrations in **Daryva.Api** or **Daryva.Data**:

### 3.1 Create a new migration (if you added/changed entities)

```powershell
cd src\Daryva.Api
dotnet ef migrations add YourMigrationName --project src/Daryva.Api --startup-project src/Daryva.Api
```

### 3.2 Apply migrations

**Local:**

```powershell
dotnet ef database update --project src/Daryva.Api --startup-project src/Daryva.Api
```

**Production (on VPS):**  
Run the same command against the production connection string, or run it once from a one-off container/job that uses the same connection string.  
(See `Docs\production-deployment.md` for env and server setup.)

---

## 4. Deploy the API to production

The API is deployed via **GitHub Actions** when certain files change on `master`, or when you run the workflow manually.

### 4.1 What triggers auto-deploy

Pushing to `master` with changes in **any** of:

- `src/Daryva.Api/**`
- `docker-compose.prod.yml`
- `.github/workflows/deploy-api.yml`

### 4.2 Steps to deploy after API changes

1. Commit your changes:

   ```powershell
   git add .
   git status
   git commit -m "Your short description of the change"
   ```

2. Push to `master`:

   ```powershell
   git push origin master
   ```

3. The **“Deploy API”** workflow will:
   - Build the API Docker image
   - Push it to GitHub Container Registry (GHCR)
   - SSH to your VPS and run:
     - `docker compose -f docker-compose.prod.yml pull api`
     - `docker compose -f docker-compose.prod.yml up -d api`

4. Check the run:
   - GitHub → **Actions** → **Deploy API** → latest run.

### 4.3 Deploy without pushing (manual run)

- GitHub → **Actions** → **Deploy API** → **Run workflow** → **Run workflow**.  
- Uses the code on the branch you select (usually `master`).  
- Ensure repo secrets are set: `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_PATH`, `DEPLOY_PORT` (optional), `GHCR_PAT`, `GHCR_USERNAME`.

### 4.4 After deploy

- Confirm: `https://api.daryva.com/health` (or your API URL) returns healthy.
- If you added migrations, run `dotnet ef database update` once against production (see §3.2).

---

## 5. Package the desktop app (Velopack installer)

When you change the **UI** and want a new Windows installer/update:

1. Install the Velopack CLI once (if not already):

   ```powershell
   dotnet tool install -g vpk
   ```

2. From repo root, run the pack script with a version:

   ```powershell
   .\Tools\Velopack-installer\build-win.ps1 -Version 1.2.3
   ```

3. Output:
   - Build: `artifacts\win-x64`
   - Installer/updates: `releases\`

See `Tools\Velopack-installer\build-win.ps1` for options (e.g. `-SkipInnoSetup`).

---

## 6. Summary checklist after making changes

- [ ] `dotnet build Daryva-Avalonia.sln` succeeds.
- [ ] (Optional) `dotnet test Daryva-Avalonia.sln` passes.
- [ ] (If schema changed) Create migration, then `dotnet ef database update` locally.
- [ ] Run locally with `.\Scripts\restart-dev.ps1` and smoke-test.
- [ ] Commit and push to `master` when ready.
- [ ] If you changed API/compose/workflow: wait for **Deploy API** to finish, then check `/health`.
- [ ] (If you changed UI and need a new installer) Run `Tools\Velopack-installer\build-win.ps1 -Version X.Y.Z`.

---

## 7. Where things live

| Item | Location |
|------|----------|
| Solution | `Daryva-Avalonia.sln` |
| API project | `src/Daryva.Api/` |
| UI project | `src/Daryva.UI/` |
| Local dev script | `Scripts/restart-dev.ps1` |
| Local Docker (Postgres) | `docker-compose.yml` |
| Production Docker | `docker-compose.prod.yml` |
| Deploy workflow | `.github/workflows/deploy-api.yml` |
| CI (build + test) | `.github/workflows/ci.yml` |
| API Dockerfile | `src/Daryva.Api/Dockerfile` |
| More deploy detail | `Docs/production-deployment.md` |
