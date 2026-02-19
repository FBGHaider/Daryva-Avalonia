# Daryva.Api - Multi-Tenant Backend Setup

## Prerequisites

- .NET 8 SDK
- Docker and Docker Compose
- PostgreSQL 16+ (via Docker, or installed locally)

## Quick Start

### 1. Start PostgreSQL via Docker Compose

From the repository root:

```bash
docker-compose up -d
```

Verify PostgreSQL is running:
```bash
docker-compose ps
```

### 2. Restore and Build

```bash
dotnet restore
dotnet build
```

### 3. Build the API

```bash
cd src/Daryva.Api
dotnet build
```

### 4. Run Database Migrations (Phase 2)

Once migrations are created:

```bash
cd src/Daryva.Api
dotnet ef database update
```

**Alternative (Manual Migration):**

```bash
psql -h localhost -U daryva -d daryva -f path-to-migration.sql
```

### 5. Run the API

```bash
cd src/Daryva.Api
dotnet run
```

API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000/swagger`
- Health Check: `GET http://localhost:5000/health`

## Configuration

### Development (\`appsettings.Development.json\`)

- **Database:** PostgreSQL on localhost:5432
- **JWT Auth:** Empty Authority (placeholder tokens OK) or DevAuth (Phase 5)
- **DevAuth:** Disabled by default

See [JWT_AUTH.md](JWT_AUTH.md) for token configuration.

### Production (\`appsettings.json\`)

- Update `ConnectionStrings.DefaultConnection` with production database
- Configure JWT Authority and Audience for your auth provider (Auth0, Azure AD B2C, Clerk, etc.)
- Set `DevAuth.Enabled` to `false`

---

## Authentication

The API uses JWT Bearer tokens. Configure your auth provider in `Jwt` settings:

```json
{
  "Jwt": {
    "Authority": "https://your-auth-provider.example.com/",
    "Audience": "daryva-api"
  }
}
```

**For local dev without auth provider:**
- Leave Authority empty (placeholder tokens accepted)
- Or enable DevAuth in Phase 5

**Request with token:**
```bash
curl -H "Authorization: Bearer <jwt_token>" \
     -H "X-Org-Id: <org-guid>" \
     http://localhost:5000/api/houses
```

See [JWT_AUTH.md](JWT_AUTH.md) for complete auth setup and examples.

---

## Multi-Tenancy: X-Org-Id Header

All org-scoped endpoints require specifying which organization:

```bash
# If user belongs to multiple orgs (required):
curl -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000" \
     http://localhost:5000/api/houses

# If user belongs to single org (auto-selected if not provided):
curl http://localhost:5000/api/houses
```

## Architecture

- **Controllers:** Request handlers, org-scoped endpoints
- **Data:** AppDbContext, entities, migrations
- **Domain:** EF Core entities (Organization, OrganizationMember, House, etc.)
- **Security:** Auth handlers, TenantContext, RBAC
- **Services:** Business logic, org isolation
- **Dtos:** Request/response models with validation

## Multi-Tenancy Model

Every request:
1. Determines `CurrentOrgId` from authenticated user + X-Org-Id header
2. All EF Core queries auto-filtered by `OrgId` (global query filters)
3. Server-side OrgId assignment on create/update (client input ignored)

## Database Connection

**Connection String:**
```
Host=localhost;Port=5432;Database=daryva;Username=daryva;Password=daryva_dev_password
```

**Override for local development:**
Create \`appsettings.Development.local.json\`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-host;Port=5432;Database=daryva;Username=your-user;Password=your-password"
  }
}
```

## Next Steps

- Phase 2: Implement DbSets, configure entities, create migrations
- Phase 3: Implement JwtBearer auth, TenantContext service
- Phase 4: Create controllers and DTOs
- Phase 5: Add development auth handler (enabled by default for local dev)

**For local development without auth provider setup:**
See [DEVAUTH.md](DEVAUTH.md) for DevAuth configuration (enabled by default in Development).
