# Daryva

Property and tenant management desktop app built with **Avalonia UI** and .NET 8. Cross-platform (Windows, macOS, Linux).

## Features

- **Dashboard** – Overview, rent due, overdue, documents expiring
- **Houses** – Properties, tenancies, house reports
- **Tenants** – Add, edit, archive; document checklist
- **Rent & Payments** – Ledger, record payments, transactions, export
- **Expenses** – Track and categorise expenses, export CSV
- **Documents** – Upload, store, and manage tenant/house documents
- **Notifications** – Compose emails, queue, schedule; test email
- **Settings** – General, theme, rent, documents, notifications, email, backup, updates

## Requirements

- .NET 8 SDK
- SQLite (default; no server required)

---

## Quick Start

```bash
cd Daryva-Avalonia
dotnet restore
dotnet build
dotnet run
```

---

## Database Setup (SQLite)

The app uses SQLite. Create the schema before first use.

### Option 1: DB Browser for SQLite (recommended)

1. Download [DB Browser for SQLite](https://sqlitebrowser.org/)
2. Create new database: `%AppData%\Daryva\Database\DaryvaDB.db` (Windows) or `~/Library/Application Support/Daryva/Database/DaryvaDB.db` (macOS)
3. Execute SQL: **File → Open SQL file** → `Database/Migrations/001_CreateDatabase_SQLite.sql` → **Execute SQL**

### Option 2: Command line

**Windows:**
```powershell
New-Item -ItemType Directory -Force -Path "$env:APPDATA\Daryva\Database"
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" < Database\Migrations\001_CreateDatabase_SQLite.sql
```

**macOS:**
```bash
mkdir -p ~/Library/Application\ Support/Daryva/Database
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db < Database/Migrations/001_CreateDatabase_SQLite.sql
```

### Option 3: Let app create file, then run migration

Run the app once to create the empty database file, then run the migration script using DB Browser or sqlite3.

---

## Configuration

Config files live in `%AppData%\Daryva\` (Windows) or `~/Library/Application Support/Daryva/` (macOS):

- `app.config.json` – Default settings
- `app.config.local.json` – Local overrides (create if needed)

### Database

Default SQLite path: `{AppData}/Daryva/Database/DaryvaDB.db`. Override in `app.config.local.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:/path/to/your/DaryvaDB.db;"
  }
}
```

### SMTP (email)

Add to `app.config.local.json`:

```json
{
  "AppSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "SmtpEnableSsl": "true",
    "SmtpFromAddress": "your-email@gmail.com"
  }
}
```

For Gmail, use an [App Password](https://myaccount.google.com/apppasswords), not your normal password.

### Updates (Velopack)

Default: `https://github.com/FBGHaider/Daryva-Updates` (GitHub Releases). Override in `app.config.local.json`:

```json
{
  "AppSettings": {
    "UpdateFeedUrl": "https://github.com/FBGHaider/Daryva-Updates"
  }
}
```

Check for updates: **Settings → General → Check for updates**. Install & Restart appears when an update is available.

---

## Data Migration (SQL Server → SQLite)

If you have existing SQL Server data:

1. **Export** from SSMS: For each table, run `SELECT * FROM TableName`, right-click results → **Save Results As** → CSV
2. **Create** SQLite database and run `001_CreateDatabase_SQLite.sql` (see Database Setup)
3. **Import** in DB Browser: **File → Import → Table from CSV file** for each CSV

**Import order** (respects foreign keys): House → Tenant → Tenancy → RentCharge → RentPayment → DepositPayment → Document → HouseExpense → Notification → NotificationTemplate → NotificationAttempt → AppSettings

Check **"Column names in first line"** when importing.

---

## Build & Release (Velopack)

Prerequisites: `dotnet tool install -g vpk`

Build scripts are in `velopack-installer/`:

| Script | Platform | Output |
|--------|----------|--------|
| `velopack-installer/build-win.ps1 [version]` | Windows (win-x64) | `artifacts/win-x64`, `releases/` |
| `velopack-installer/build-mac.sh [version]` | macOS Apple Silicon | `artifacts/osx-arm64`, `releases/` |
| `velopack-installer/build-mac-intel.sh [version]` | macOS Intel | `artifacts/osx-x64`, `releases/` |

Example:
```powershell
.\velopack-installer\build-win.ps1 1.0.0
```

```bash
./velopack-installer/build-mac.sh 1.0.0
```

Upload the `releases/` contents to [GitHub Releases](https://github.com/FBGHaider/Daryva-Updates/releases) for auto-updates.

---

## File Locations

| Platform | App Data | Database |
|----------|----------|----------|
| **Windows** | `%AppData%\Daryva\` | `%AppData%\Daryva\Database\DaryvaDB.db` |
| **macOS** | `~/Library/Application Support/Daryva/` | Same path + `Database/DaryvaDB.db` |
| **Linux** | `~/.config/Daryva/` | Same path + `Database/DaryvaDB.db` |

Exports: `~/Documents/Daryva Exports/` (or user-selected path)

---

## Project Layout

- `Daryva-Avalonia/` – Avalonia app (MVVM, services, themes)
- `Database/Migrations/` – SQLite schema and migrations
- `velopack-installer/` – Velopack build scripts (build-win.ps1, build-mac.sh, build-mac-intel.sh)

---

## Troubleshooting

**Database file locked** – Close DB Browser, VS Code, or any app using the database.

**Updates not available** – Updates only work when the app is installed via Velopack (not when running from source with `dotnet run`).

**macOS Gatekeeper** – Right-click the app → **Open** (first launch).

**macOS signing/notarization** – Required for distribution. See [Velopack macOS docs](https://docs.velopack.io/packaging/operating-systems/macos).
