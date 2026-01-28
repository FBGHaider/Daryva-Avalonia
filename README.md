# Daryva (Avalonia)

Property and tenant management desktop app built with **Avalonia UI** and .NET 8.

## Features

- **Dashboard** – Overview, rent due, overdue, documents expiring
- **Houses** – Properties, tenancies, house reports
- **Tenants** – Add, edit, archive; document checklist
- **Rent & Payments** – Ledger, record payments, transactions, export
- **Expenses** – Track and categorise expenses, export CSV
- **Documents** – Upload, store, and manage tenant/house documents
- **Notifications** – Compose emails, queue, schedule; test email
- **Settings** – General, theme, rent, documents, notifications, email, backup

## Requirements

- .NET 8 SDK
- SQL Server (local or Docker)

## Run

1. **Database**
   - Use **Docker**: see [README-Docker.md](README-Docker.md).
   - Or **direct SQL Server**: see [README-DirectDB.md](README-DirectDB.md).

2. **Configure** (optional)
   - Copy `Daryva-Avalonia/App.config.local.example` to `Daryva-Avalonia/App.config.local`.
   - Set SMTP (email), connection overrides, etc. See **[CONFIGURATION.md](CONFIGURATION.md)** for examples.  
   - `App.config.local` is gitignored.

3. **Build & run**
   ```bash
   dotnet build Daryva-Avalonia.sln
   dotnet run --project Daryva-Avalonia/Daryva.csproj
   ```

## Project layout

- `Daryva-Avalonia.sln` – Solution file (single “Daryva Avalonia” app)
- `Daryva-Avalonia/` – Avalonia app (MVVM, services, themes)
- `Database/` – SQL migrations and scripts
- [CONFIGURATION.md](CONFIGURATION.md) – Database, SMTP, and config examples (use examples only; keep real credentials in `App.config.local`)

## License

Private.
