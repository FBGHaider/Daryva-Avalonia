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

### Migrations (existing databases)

The app runs migrations automatically on startup. If you prefer to run manually:
- **016_AddRentStartAndBackfillMoveIn.sql** – Adds RentStartMonth/RentStartYear to Tenancy, backfills MoveInDate from first payment

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

### Which installer for GitHub updates?

| Installer | GitHub updates | Wizard (terms, options) |
|-----------|----------------|--------------------------|
| **`Daryva-Setup-{version}.exe`** (Inno hybrid) | ✅ Yes | Full wizard (Welcome, License, Options, Ready, Installing, Finished) |
| `FBGHaider.Daryva-win-Setup.exe` | ✅ Yes | Basic (Velopack default) |

**Recommended:** `Daryva-Setup-{version}.exe` gives you both the full wizard and GitHub updates. It runs Velopack Setup.exe during install (with `--silent` to avoid full-screen splash). Velopack installs to `%LocalAppData%\FBGHaider.Daryva`, enabling Check for updates in Settings → General.

**Build:** Run `.\velopack-installer\build-win.ps1 -Version 1.0.0` (full build). Upload `Daryva-Setup-{version}.exe` and the Velopack files to [Daryva-Updates releases](https://github.com/FBGHaider/Daryva-Updates/releases).

### Branded Installer (no updates)

**Windows:** With [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed, the build also produces `Daryva-Setup-{version}.exe` – a full wizard installer that installs directly from published artifacts:
- **Welcome** – Logo and slogan (Next/Back)
- **License** – Terms & conditions (I accept / Next / Back)
- **Destination** – Choose install folder (Next/Back)
- **Additional Options** – "Create desktop shortcut" checkbox (Next/Back)
- **Ready** – Review and Install (Install/Back)
- **Installing** – Progress bar
- **Finished** – "Open Daryva" checkbox (Finish)

Use `-SkipInnoSetup` to skip the Inno wizard and build only `FBGHaider.Daryva-win-Setup.exe` (recommended for update-enabled distribution).

**macOS:** The `.pkg` installer includes Welcome, License (terms), and Conclusion pages. Users can choose `/Applications` or `~/Applications`.

**Assets:** Customize `velopack-installer/installer-assets/`:
- `logo.png` or `splash.png` – used to generate `logo-small.bmp` (smaller logo in progress window)
- `terms.rtf`, `welcome.rtf`, `conclusion.rtf` – macOS pkg pages

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
- `velopack-installer/` – Velopack build scripts and installer assets (logo, terms, welcome)

---

## Troubleshooting

**Database file locked** – Close DB Browser, VS Code, or any app using the database.

**Updates not available** – Updates only work when the app is installed via Velopack (not when running from source with `dotnet run`).

**macOS Gatekeeper** – Right-click the app → **Open** (first launch).

**macOS signing/notarization** – Required for distribution. See [Velopack macOS docs](https://docs.velopack.io/packaging/operating-systems/macos).
