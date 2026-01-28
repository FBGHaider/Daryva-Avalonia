# Using a Direct Database (No Docker)

Daryva can connect to SQL Server **without Docker**. Use a local SQL Server instance (LocalDB, SQL Server Express, or a full instance) and point the app at it. The database starts automatically with Windows or when you first connect—no need to run Docker.

## Quick setup

1. **Install SQL Server** (one of):
   - **LocalDB** – lightweight, often installed with Visual Studio. Good for dev.
   - **SQL Server Express** – free, runs as a Windows service.

2. **Create the database**  
   Run the project’s migrations or scripts to create `DaryvaDB` on your local instance (same as you would for Docker).

3. **Override the connection string**  
   Use `App.config.local` so the app uses your local DB instead of Docker:

   - Copy `Daryva/App.config.local.example` to `Daryva/App.config.local`.
   - In `App.config.local`, add a `connectionStrings` section and set `DefaultConnection` to your local instance.

### LocalDB example

```xml
<configuration>
  <connectionStrings>
    <add name="DefaultConnection"
         connectionString="Server=(localdb)\MSSQLLocalDB;Database=DaryvaDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
         providerName="Microsoft.Data.SqlClient" />
  </connectionStrings>
</configuration>
```

Create `DaryvaDB` on LocalDB (e.g. via SSMS or `SqlCmd`) before running the app.

### SQL Server Express example

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Server=localhost\SQLEXPRESS;Database=DaryvaDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
       providerName="Microsoft.Data.SqlClient" />
</connectionStrings>
```

Or with SQL authentication:

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Server=localhost;Database=DaryvaDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True;"
       providerName="Microsoft.Data.SqlClient" />
</connectionStrings>
```

## How it works

- **App.config** defines the default connection (Docker `localhost,1433`).
- **App.config.local** overrides that when it contains a `DefaultConnection` entry under `connectionStrings`.
- The app never reads Docker-specific config; it only uses the effective connection string.  
  So you can switch between Docker and a direct DB by changing config only.

## Check database status

Run from the repo root:

```powershell
.\check-database.ps1
```

This reports:

- Docker container status (if you use Docker).
- Whether `localhost:1433` is reachable.
- Which config (App.config vs App.config.local) is used for the connection string.

## Summary

| Setup | Connection | Docker required? |
|-------|------------|------------------|
| Docker (default) | `localhost,1433` in App.config | Yes |
| LocalDB | Override in App.config.local | No |
| SQL Server Express | Override in App.config.local | No |

Use a direct database when you prefer a locally installed SQL Server and don’t want to run Docker.
