# Daryva

Property and tenant management desktop app built with Avalonia UI and .NET 8, backed by a multi-tenant ASP.NET Core API (PostgreSQL).

## Features

- Dashboard: rent due, overdue, expiring docs
- Houses: properties, tenancies, reports
- Tenants: add/edit/archive, checklists
- Payments: ledger, transactions, exports
- Expenses: tracking and CSV export
- Documents: upload and manage
- Notifications: compose, queue, schedule (email supported)

## Requirements

- .NET 8 SDK
- SQLite (local UI data)
- PostgreSQL (API)
- Docker (for local PostgreSQL via compose)

## Quick Start

```powershell
docker-compose up -d

cd src\Daryva.Api
dotnet run

cd ..\Daryva.UI
dotnet run
```

API: http://localhost:5000
Swagger: http://localhost:5000/swagger

## Production (daryva.com)

- Deployment runbook: `Docs/production-deployment.md`
- Production compose template: `docker-compose.prod.yml`
- Production env template: `.env.prod.example`
- API container image build file: `src/Daryva.Api/Dockerfile`
- Nginx reverse proxy config: `deploy/nginx/daryva.conf`
- Nginx setup guide: `deploy/nginx/README.md`
- GitHub Actions API deploy workflow: `.github/workflows/deploy-api.yml`
- Launch-week checklist: `Docs/launch-this-week.md`

## Configuration

### UI (local config)

Create `%AppData%\Daryva\app.config.local.json`:

```json
{
  "AppSettings": {
    "ApiBaseUrl": "http://localhost:5000",
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "SmtpEnableSsl": "true",
    "SmtpFromAddress": "your-email@gmail.com"
  }
}
```

Gmail requires an App Password: https://myaccount.google.com/apppasswords

### API (SMTP)

Set in `src/Daryva.Api/appsettings.Development.json`:

```json
{
  "Smtp": {
    "Server": "smtp.gmail.com",
    "Port": "587",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "EnableSsl": "true",
    "FromAddress": "your-email@gmail.com"
  }
}
```

## Multi-Tenancy

- If a user belongs to multiple orgs, add `X-Org-Id` header to requests.
- DevAuth is enabled in development by default (auto user: dev@local).

## Data Migration (SQLite -> API)

- Use the in-app Migration screen to push local SQLite data into the API.
- Import includes houses, tenants, tenancies, expenses, documents, payments, and notifications.

## Notifications

- Channels: Email, SMS, WhatsApp (SMS/WhatsApp are not configured yet).
- Email sending happens in the API using SMTP settings above.
- All scheduled timestamps are normalized to UTC for PostgreSQL.

## API Endpoints (Summary)

- Orgs: `/api/orgs`
- Houses: `/api/houses`
- Tenants: `/api/tenants`
- Expenses: `/api/expenses`
- Documents: `/api/documents`
- Notifications: `/api/notifications`
- Templates: `/api/notification-templates`
- Import (dev): `/api/import`
- Health: `/health`

## Build & Release (Velopack)

Build scripts are in `Tools/Velopack-installer/`:

- `build-win.ps1 <version>`
- `build-mac.sh <version>`
- `build-mac-intel.sh <version>`

Example (local build only):

```powershell
Tools\Velopack-installer\build-win.ps1 1.0.0
```

### Publishing a release so users can update

To publish a new desktop version to the **Daryva-Updates** repo (so installed clients get "Check for updates"):

1. **One-time setup:** In this repo (Daryva-Avalonia), add a secret **`DARYVA_UPDATES_GITHUB_TOKEN`** (Settings → Secrets and variables → Actions): a GitHub Personal Access Token with `repo` scope that can create releases and upload assets on the **FBGHaider/Daryva-Updates** repository.
2. **Create the release:**
   - **Option A (recommended):** In GitHub, open **Actions → "Release desktop to Daryva-Updates"** → **Run workflow**, enter the version (e.g. `1.0.1`) and run. The workflow builds the app, packs with Velopack, and publishes the release (and assets) to Daryva-Updates.
   - **Option B:** Push a tag: `git tag v1.0.1 && git push origin v1.0.1`. The same workflow runs and uses the tag as the version.

The release tag on Daryva-Updates will be **vX.Y.Z** (e.g. `v1.0.1`). The app’s update feed points at that repo, so users will see the new version when they use **Settings → Check for updates**.

## File Locations

- App data: `%AppData%\Daryva\`
- SQLite DB: `%AppData%\Daryva\Database\DaryvaDB.db`

---

## Project Layout

- `src/Daryva.UI/` – Avalonia app (MVVM, services, themes)
- `src/Daryva.Data/Migrations/` – SQLite schema and migrations
- `Tools/Velopack-installer/` – Velopack build scripts and installer assets (logo, terms, welcome)

---

## Troubleshooting

**Database file locked** – Close DB Browser, VS Code, or any app using the database.

**Updates not available** – Updates only work when the app is installed via Velopack (not when running from source with `dotnet run`).

**macOS Gatekeeper** – Right-click the app → **Open** (first launch).

**macOS signing/notarization** – Required for distribution. See [Velopack macOS docs](https://docs.velopack.io/packaging/operating-systems/macos).
