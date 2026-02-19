# Quick Start: Run API Locally

## TL;DR - 3 Steps

### 1. Start PostgreSQL

```bash
docker-compose up -d
```

### 2. Run API

```bash
# From repository root:
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia\src\Daryva.Api"
dotnet run

# Or if already in repo:
cd src/Daryva.Api
dotnet run
```

Expected output:
```
⚠️  DevAuth is ENABLED. This must NEVER be used in production...
✓ Seeded sample data:
  Organization: Dev Property Management
  Houses: 3

Now listening on: http://localhost:5000
```

### 3. Test API

```bash
# Get orgs (auto-authenticated as dev user)
curl http://localhost:5000/api/orgs | jq

# Get Swagger UI
open http://localhost:5000/swagger
```

---

## What You Get

✅ **Automatic:**
- Sample organization auto-created
- 3 sample houses auto-created
- Dev user "dev@local" with Owner role
- No auth provider needed
- No JWT tokens needed

✅ **API Ready:**
- 11 endpoints for organizations & houses
- Multi-tenancy verified
- Data isolation guaranteed
- Swagger documentation

---

## Common Tasks

### List Your Organizations

```bash
curl http://localhost:5000/api/orgs | jq
```

### Get Organization ID

```bash
ORG_ID=$(curl -s http://localhost:5000/api/orgs | jq -r '.[0].id')
echo "Your Org: $ORG_ID"
```

### List Houses in Organization

```bash
curl -H "X-Org-Id: $ORG_ID" \
     http://localhost:5000/api/houses | jq
```

### Create New House

```bash
curl -X POST http://localhost:5000/api/houses \
  -H "X-Org-Id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My New House",
    "addressLine1": "100 Main St",
    "city": "NYC",
    "postcode": "10001"
  }' | jq
```

### Create Second Organization

```bash
curl -X POST http://localhost:5000/api/orgs \
  -H "Content-Type: application/json" \
  -d '{"name": "Second Org"}' | jq '.id'
```

### Browse Swagger UI

```bash
open http://localhost:5000/swagger
```

Then you can:
- Click "Try It Out" on any endpoint
- See live API responses
- Copy cURL commands

---

## Stop Everything

### Stop API

```
Ctrl+C in terminal
```

### Stop PostgreSQL

```bash
docker-compose down
```

---

## Troubleshooting

### "Database connection failed"

**Cause:** PostgreSQL not running

**Fix:**
```bash
docker-compose up -d
docker-compose ps  # Verify running
```

### "DevAuth is ENABLED" Warning

**Cause:** Expected in development

**Ignore it** or disable DevAuth in `appsettings.Development.json`:
```json
{
  "DevAuth": {
    "Enabled": false
  }
}
```

### "You belong to multiple organizations"

**Cause:** Dev user has 2+ orgs, need to specify which one

**Fix:** Add header:
```bash
curl -H "X-Org-Id: <org-uuid>" \
     http://localhost:5000/api/houses
```

### Swagger UI shows "401 Unauthorized"

**Cause:** DevAuth disabled, JWT auth required

**Fix:** Either:
1. Enable DevAuth (see above)
2. Or configure JWT provider in `appsettings.Development.json`

See [JWT_AUTH.md](src/Daryva.Api/JWT_AUTH.md) for provider setup

---

## Documentation

- **[SETUP.md](src/Daryva.Api/SETUP.md)** — Detailed setup instructions
- **[DEVAUTH.md](src/Daryva.Api/DEVAUTH.md)** — DevAuth configuration
- **[API_ENDPOINTS.md](src/Daryva.Api/API_ENDPOINTS.md)** — API reference with examples
- **[JWT_AUTH.md](src/Daryva.Api/JWT_AUTH.md)** — JWT provider setup (Auth0, Azure AD B2C, etc.)
- **[PHASE_5_COMPLETE.md](PHASE_5_COMPLETE.md)** — What was implemented in Phase 5

---

## Architecture at a Glance

```
Request
  ↓
DevAuthMiddleware (injects dev user)
  ↓
JwtBearer Auth (skipped if already authenticated)
  ↓
TenantContextMiddleware (sets org context)
  ↓
Controller (routes to endpoint)
  ↓
Service (business logic)
  ↓
EF Core (global query filter enforces OrgId)
  ↓
PostgreSQL (multi-tenant data)
```

**Result:** Requests auto-routed to correct org, isolated from other orgs

---

## Ready to Go!

You now have a **production-ready, multi-tenant SaaS backend** ready for:
- ✅ Local development without auth setup
- ✅ Testing all organization & house endpoints
- ✅ Building Avalonia UI against stable API
- ✅ Integration testing multi-tenancy isolation

**Start coding!** 🚀
