# Database Migration Instructions

## Overview

These migrations set up the PostgreSQL database schema for Daryva API with multi-tenant architecture.

**Migration:** `InitialCreate` - Creates Organizations, OrganizationMembers, and Houses tables with proper constraints, indexes, and FK relationships.

## Prerequisites

1. **PostgreSQL 16+** running (via Docker Compose or locally)
2. **.NET 8 SDK** installed
3. **Database credentials:**
   - Host: `localhost`
   - Port: `5432`
   - Database: `daryva`
   - User: `daryva`
   - Password: `daryva_dev_password` (development only)

## Option 1: EF Core CLI (Recommended for Development)

### 1. Start PostgreSQL (if using Docker)

From repository root:
```bash
docker-compose up -d
```

Verify it's running:
```bash
docker-compose ps
```

### 2. Apply Migrations

From repository root:
```bash
dotnet ef database update --project src/Daryva.Api --startup-project src/Daryva.Api
```

This will:
- Create the database if it doesn't exist
- Run all pending migrations in order
- Create all tables, indexes, and constraints

### 3. Verify

Check PostgreSQL directly (optional):
```bash
psql -h localhost -U daryva -d daryva -c "\dt"
```

You should see three tables:
- `Organizations`
- `OrganizationMembers`
- `Houses`

## Option 2: Manual SQL (Production or Manual Control)

### 1. Generate SQL Script

```bash
dotnet ef migrations script --project src/Daryva.Api --output migrations.sql
```

### 2. Apply SQL Script

```bash
psql -h localhost -U daryva -d daryva -f migrations.sql
```

Or in PostgreSQL client:
```sql
\i migrations.sql
```

## Connection String Override

If you need to use a different database, create `src/Daryva.Api/appsettings.Development.local.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-host;Port=5432;Database=your-db;Username=your-user;Password=your-password"
  }
}
```

## Database Schema

### Organizations Table
- `Id` (UUID, PK)
- `Name` (VARCHAR(256), NOT NULL)
- `CreatedAt` (TIMESTAMP WITH TIME ZONE, default CURRENT_TIMESTAMP)

### OrganizationMembers Table
- `Id` (UUID, PK)
- `OrganizationId` (UUID, FK → Organizations.Id, ON DELETE CASCADE)
- `UserId` (VARCHAR(256), NOT NULL)
- `Email` (VARCHAR(256), nullable)
- `Role` (VARCHAR(50), NOT NULL) - Values: "Owner", "Admin", "Member", "ReadOnly"
- `JoinedAt` (TIMESTAMP WITH TIME ZONE, default CURRENT_TIMESTAMP)
- **Unique Index:** `(OrganizationId, UserId)` — One user can have only one role per org

### Houses Table
- `Id` (UUID, PK)
- `OrganizationId` (UUID, FK → Organizations.Id, ON DELETE CASCADE) — **CRITICAL FOR MULTI-TENANCY**
- `Name` (VARCHAR(256), NOT NULL)
- `AddressLine1` (VARCHAR(256), NOT NULL)
- `AddressLine2` (VARCHAR(256), nullable)
- `City` (VARCHAR(128), NOT NULL)
- `Postcode` (VARCHAR(20), NOT NULL)
- `CreatedAt` (TIMESTAMP WITH TIME ZONE, default CURRENT_TIMESTAMP)
- **Index:** `OrganizationId` — Optimizes global query filter performance

## Multi-Tenancy Security

- **Global Query Filter:** All `House` queries automatically filtered by `OrganizationId` via EF Core
- **Cascade Deletes:** Deleting an org deletes all its members and houses
- **Unique Constraint:** One user per role per org (prevents duplicate memberships)
- **DbContext Enforcement:** No raw SQL or unfiltered queries bypass the isolation

## Rollback

To undo migrations (remove all tables):

```bash
dotnet ef database update 0 --project src/Daryva.Api --startup-project src/Daryva.Api
```

To remove a specific migration (dev only):

```bash
dotnet ef migrations remove --project src/Daryva.Api
```

## Troubleshooting

**Connection refused?**
- Check PostgreSQL is running: `docker-compose ps`
- Verify credentials in `appsettings.Development.json`

**"Model compatibility" error?**
- Ensure you're using the latest EF Core: `dotnet add package Microsoft.EntityFrameworkCore@latest`

**Tables already exist but schema is wrong?**
- Back up data, then: `dotnet ef database drop --project src/Daryva.Api --force`
- Re-apply: `dotnet ef database update --project src/Daryva.Api --startup-project src/Daryva.Api`

## Next Steps

- Phase 3: Implement JWT auth middleware and TenantContext request determination
- Phase 4: Create API controllers and DTOs
- Phase 5: Add development auth handler for local testing
