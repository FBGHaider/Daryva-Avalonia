# Phase 5: Development Authentication

## Overview

**Phase 5** enables seamless local development without requiring an external authentication provider. When enabled, DevAuth middleware injects a fake user identity, allowing you to:

✅ Test API endpoints without JWT provider setup
✅ Auto-seed sample data (organization + houses)
✅ Focus on business logic, not auth infrastructure

**⚠️ WARNING:** DevAuth is **development-only** and must NEVER be enabled in production.

---

## What's Included

### 1. DevAuthMiddleware

**File:** [Security/DevAuthMiddleware.cs](Security/DevAuthMiddleware.cs)

Injects a fake ClaimsPrincipal when `DevAuth.Enabled: true`:

```csharp
claims = [
  ("sub", "dev-user-1"),
  ("email", "dev@local"),
  ("name", "Dev User")
]
```

Always runs BEFORE JWT authentication, so:
- If DevAuth enabled → request auto-authenticated as dev user
- If DevAuth disabled → falls back to JWT Bearer auth

### 2. DataSeeder Service

**File:** [Services/Seed/DataSeeder.cs](Services/Seed/DataSeeder.cs)

Automatically creates sample data on API startup:
- Organization: "Dev Property Management"
- User: "dev@local" (Role: Owner)
- Houses: 3 sample properties

**Auto-runs on startup** if `DevAuth.Enabled: true`

**Optional:** Call `/api/seed` endpoint to re-seed if needed

### 3. SeedController

**File:** [Controllers/SeedController.cs](Controllers/SeedController.cs)

Endpoint for manual seeding:

```
POST /api/seed
```

Response:
```json
{
  "message": "Sample data seeded successfully",
  "devAuthEnabled": true,
  "devUserId": "dev-user-1",
  "devUserEmail": "dev@local"
}
```

### 4. Configuration

**File:** [appsettings.Development.json](appsettings.Development.json)

```json
{
  "DevAuth": {
    "Enabled": true,
    "UserId": "dev-user-1",
    "Email": "dev@local",
    "Name": "Dev User"
  }
}
```

---

## Quick Start

### 1. Start API

```bash
cd src/Daryva.Api
dotnet run
```

**Console Output:**
```
⚠️  DevAuth is ENABLED. This must NEVER be used in production. Requests will be authenticated as 'dev@local'.
✓ Seeded sample data:
  Organization: Dev Property Management (ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
  Member: dev@local (Role: Owner)
  Houses: 3
```

### 2. Call API (No Token Needed)

```bash
# Get all orgs (auto-authenticated as dev user)
curl http://localhost:5000/api/orgs

# Get all houses for org
curl -H "X-Org-Id: <org-id>" \
     http://localhost:5000/api/houses

# Create a new house
curl -X POST http://localhost:5000/api/houses \
  -H "X-Org-Id: <org-id>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "New Property",
    "addressLine1": "100 Example Ave",
    "city": "Example City",
    "postcode": "12345"
  }'
```

### 3. View in Swagger

Open browser: `http://localhost:5000/swagger`

All endpoints are available without clicking "Authorize" button.

---

## Architecture

```
Request
  ↓
[DevAuthMiddleware] ← Injects fake user if enabled
  ↓
[JwtBearer Auth] ← Real auth if DevAuth disabled
  ↓
[TenantContextMiddleware] ← Sets CurrentOrgId from X-Org-Id header
  ↓
[Controllers] ← Process request as authenticated user
```

---

## Configuration Options

### Enable/Disable DevAuth

Edit [appsettings.Development.json](appsettings.Development.json):

```json
{
  "DevAuth": {
    "Enabled": true  // ← Set to false to use real JWT auth
  }
}
```

### Customize Dev User

```json
{
  "DevAuth": {
    "Enabled": true,
    "UserId": "dev-user-1",      // Claim: "sub"
    "Email": "dev@local",        // Claim: "email"
    "Name": "Dev User"           // Claim: "name"
  }
}
```

---

## Testing Scenarios

### 1. Automatic Seeding

```bash
# Start API
dotnet run

# Wait for log: ✓ Seeded sample data

# Query orgs (dev user is owner)
curl http://localhost:5000/api/orgs
```

Response:
```json
[
  {
    "id": "uuid-of-seeded-org",
    "name": "Dev Property Management",
    "createdAt": "2026-02-19T...",
    "currentUserRole": "Owner"
  }
]
```

### 2. Create New Organization

```bash
curl -X POST http://localhost:5000/api/orgs \
  -H "Content-Type: application/json" \
  -d '{"name": "My Test Org"}'
```

Response: `201 Created` with new org

### 3. Multi-Tenancy (Multiple Orgs)

```bash
# Now dev user is owner of 2 orgs

# Try to list houses without X-Org-Id
curl http://localhost:5000/api/houses

# Response: 400 Bad Request
# {
#   "error": "Bad Request",
#   "message": "You belong to multiple organizations. Specify X-Org-Id header."
# }

# Specify org explicitly
curl -H "X-Org-Id: <org-id>" \
     http://localhost:5000/api/houses
```

### 4. House CRUD

```bash
ORG_ID="<copy-from-orgs-response>"

# Create house
curl -X POST http://localhost:5000/api/houses \
  -H "X-Org-Id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test House",
    "addressLine1": "123 Test St",
    "city": "Test City",
    "postcode": "12345"
  }'

# List houses
curl -H "X-Org-Id: $ORG_ID" \
     http://localhost:5000/api/houses

# Update house
curl -X PUT http://localhost:5000/api/houses/<house-id> \
  -H "X-Org-Id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{"name": "Updated House"}'

# Delete house
curl -X DELETE http://localhost:5000/api/houses/<house-id> \
  -H "X-Org-Id: $ORG_ID"
```

---

## Disabling DevAuth (Using Real JWT Auth)

### 1. Disable in appsettings.Development.json

```json
{
  "DevAuth": {
    "Enabled": false
  }
}
```

### 2. Configure JWT Authority

```json
{
  "Jwt": {
    "Authority": "https://your-auth-provider.example.com/",
    "Audience": "daryva-api"
  }
}
```

See [JWT_AUTH.md](JWT_AUTH.md) for provider setup (Auth0, Azure AD B2C, Clerk, etc.)

### 3. Start API

```bash
dotnet run
```

Now requests require valid JWT Bearer tokens.

```bash
curl -H "Authorization: Bearer <valid-token-from-auth-provider>" \
     http://localhost:5000/api/orgs
```

---

## Files Modified/Created

✅ **New Files:**
- `Security/DevAuthMiddleware.cs` — Injects fake user identity
- `Services/Seed/IDataSeeder.cs` — Interface for seeding
- `Services/Seed/DataSeeder.cs` — Creates sample org & houses
- `Controllers/SeedController.cs` — Manual seed endpoint

✅ **Modified Files:**
- `Program.cs` — Register DevAuth middleware & DataSeeder service
- `appsettings.Development.json` — Enable DevAuth by default

---

## Build Status

✅ All 6 projects compile successfully (0 errors)
✅ API ready for local development without auth provider
✅ Sample data auto-seeded on startup

---

## Next Steps

1. **Integration Tests:** Write tests proving org isolation
2. **RBAC Enforcement:** Only Owners/Admins can add members
3. **Admin Endpoints:** Delete members, change roles
4. **Production Deployment:** Remove DevAuth, configure real JWT provider
5. **Client Integration:** Build Avalonia UI consuming these endpoints

---

## Troubleshooting

### "DevAuth is ENABLED" Warning

This is expected in Development. It's a reminder that DevAuth must NEVER be enabled in production.

```
⚠️  DevAuth is ENABLED. This must NEVER be used in production.
```

**To suppress:** Set `DevAuth.Enabled: false`

### Seeding Creates Duplicate Data

The DataSeeder checks if dev user already has an org before seeding:

```csharp
var existingOrg = await _dbContext.Organizations
    .FirstOrDefaultAsync(o => o.Members.Any(m => m.UserId == "dev-user-1"));

if (existingOrg != null)
{
    return; // Already seeded
}
```

**To force re-seed:** Call `POST /api/seed` endpoint or delete database and restart.

### JWT Options Not Recognized

If you see "Jwt is required", make sure `appsettings.Development.json` includes:

```json
{
  "Jwt": {
    "Authority": "",
    "Audience": "daryva-api"
  }
}
```

Empty Authority is valid for DevAuth mode (no validation).

---

## Security Notes

⚠️ **CRITICAL:**

- DevAuth MUST be disabled in production
- Never commit production credentials to version control
- Use environment variables for sensitive config in production
- Verify X-Org-Id header is validated server-side (automatic via TenantContextMiddleware)
- All queries auto-filtered by OrganizationId (global query filter in AppDbContext)

