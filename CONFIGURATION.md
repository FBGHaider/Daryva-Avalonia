# Daryva configuration

This document describes how to configure **database**, **SMTP (email)**, and **other settings**. Use **example values only**; put your real credentials in `Daryva-Avalonia/App.config.local` (gitignored).

---

## App.config.local (sensitive overrides)

- **Location:** `Daryva-Avalonia/App.config.local`
- **Created from:** Copy `Daryva-Avalonia/App.config.local.example` → `Daryva-Avalonia/App.config.local`
- **Git:** `App.config.local` is gitignored. Never commit it.

The app reads **connection strings** and **SMTP** from `App.config` first, then overrides with `App.config.local` if present. Use `App.config.local` for:

- Your real database connection (when not using Docker default)
- Your real SMTP credentials

After editing `App.config.local`, rebuild so it is copied to the output folder.

---

## Database

### Docker (default)

If you use `docker-compose up -d` with the project’s `docker-compose.yml`:

| Setting   | Value                |
|----------|----------------------|
| **Server** | `localhost,1433`   |
| **Database** | `DaryvaDB`      |
| **User** | `sa`                 |
| **Password** | `YourStrong@Password123` |

**Connection string (example):**
```
Server=localhost,1433;Database=DaryvaDB;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True;
```

This matches `App.config`. **Change the password** in both `docker-compose.yml` and your config for production.

### LocalDB (no Docker)

| Setting   | Example                    |
|----------|----------------------------|
| **Server** | `(localdb)\MSSQLLocalDB` |
| **Database** | `DaryvaDB`             |
| **Auth** | Integrated Security        |

**Connection string (example):**
```
Server=(localdb)\MSSQLLocalDB;Database=DaryvaDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

Put this in `App.config.local` under `connectionStrings` → `DefaultConnection` to override the Docker default.

### SQL Server Express

**Windows authentication:**
```
Server=localhost\SQLEXPRESS;Database=DaryvaDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

**SQL authentication (example user/password):**
```
Server=localhost;Database=DaryvaDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True;
```

Replace `YourPassword` with your actual SA (or other) password. Use `App.config.local` for the real connection string.

---

## SMTP (email)

Store SMTP settings in `App.config.local` under `appSettings`. The app uses:

- `SmtpServer`
- `SmtpPort`
- `SmtpUsername`
- `SmtpPassword`
- `SmtpEnableSsl` (`true` / `false`)
- `SmtpFromAddress` (optional; defaults to `SmtpUsername`)

### Gmail (example)

| Key | Example value |
|-----|----------------|
| `SmtpServer` | `smtp.gmail.com` |
| `SmtpPort` | `587` |
| `SmtpUsername` | `your-email@gmail.com` |
| `SmtpPassword` | *App Password* (not your normal password) |
| `SmtpEnableSsl` | `true` |
| `SmtpFromAddress` | `your-email@gmail.com` |

**App Password:** Enable 2-Step Verification, then create an App Password at [Google App Passwords](https://myaccount.google.com/apppasswords). Use that in `SmtpPassword`.

### Outlook / Hotmail (example)

| Key | Example value |
|-----|----------------|
| `SmtpServer` | `smtp-mail.outlook.com` |
| `SmtpPort` | `587` |
| `SmtpUsername` | `your-email@outlook.com` |
| `SmtpPassword` | your account password |
| `SmtpEnableSsl` | `true` |
| `SmtpFromAddress` | `your-email@outlook.com` |

### App.config.local example (SMTP block)

```xml
<appSettings>
  <add key="SmtpServer" value="smtp.gmail.com" />
  <add key="SmtpPort" value="587" />
  <add key="SmtpUsername" value="your-email@gmail.com" />
  <add key="SmtpPassword" value="your-app-password" />
  <add key="SmtpEnableSsl" value="true" />
  <add key="SmtpFromAddress" value="your-email@gmail.com" />
</appSettings>
```

---

## Other configuration

- **Document storage path** – Configurable in **Settings → Documents**. Default: `Documents` under the app directory, or a path you choose.
- **Backup location** – Configurable in **Settings → Data & backup**. Default: `%AppData%\Daryva\Backups`.
- **Theme, date format, currency, notifications, etc.** – Stored in app settings (JSON) and via **Settings** in the UI. No config file edits needed.

---

## Summary

| What | Where |
|------|--------|
| Database (Docker default) | `App.config` |
| Database (your instance) | `App.config.local` → `connectionStrings` |
| SMTP / email | `App.config.local` → `appSettings` |
| Documents, backups, etc. | Settings UI (or app settings store) |

Use **example** values in this document; keep **real** usernames, passwords, and API keys only in `App.config.local`.
