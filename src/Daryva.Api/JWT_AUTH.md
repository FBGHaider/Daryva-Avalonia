# JWT Bearer Authentication Setup

## Overview

Daryva API uses JWT Bearer tokens for secure, stateless authentication. The system supports:
- **Production:** JWT tokens from Auth0, Azure AD B2C, Clerk, or any OpenID Connect provider
- **Development:** JWT placeholder (empty Authority) or DevAuth mode (Phase 5)

## Architecture

### Request Flow

```
1. Client sends HTTP request with JWT Bearer token:
   Authorization: Bearer <jwt_token>

2. ASP.NET Core middleware validates JWT:
   - Fetches public keys from Authority's JWKS endpoint
   - Validates signature, issuer, audience, expiration
   - Extracts claims (sub, email, etc.)

3. Custom TenantContextMiddleware runs:
   - Reads X-Org-Id header (optional org selection)
   - Validates user's membership in requested org OR auto-selects single org
   - Sets CurrentOrgId in TenantContext (injected into DbContext)

4. Request reaches controller:
   - Controller is authorized (authenticated user)
   - All database queries auto-filtered by CurrentOrgId (global query filter)
   - Even if code forgets WHERE clause, EF Core appends it automatically

5. Response includes proper CORS, caching, security headers
```

### Security Guarantees

✅ **No Token Injection:** OrgId always comes from CurrentOrgId (set by middleware), never from request body
✅ **Global Query Filters:** Even accidental code paths can't leak data across orgs
✅ **Membership Validation:** User can only access orgs they belong to
✅ **Stateless:** No session database; trust the JWT signature and Authority

---

## Configuration: Production (Auth0, Azure AD B2C, Clerk)

### 1. Register Your API

**Auth0 example:**
- Go to [Auth0 Dashboard](https://manage.auth0.com) → Applications → APIs
- Create new API:
  - Name: "Daryva API"
  - Identifier: `https://daryva-api.example.com` (or your domain)
  - Signing Algorithm: RS256

**Result:**
- Authority: `https://your-tenant.auth0.com/`
- Audience: `https://daryva-api.example.com`

### 2. Configure API Settings

#### appsettings.json (Production)

```json
{
  "Jwt": {
    "Authority": "https://your-tenant.auth0.com/",
    "Audience": "https://daryva-api.example.com"
  }
}
```

#### appsettings.Development.json (Local Dev)

```json
{
  "Jwt": {
    "Authority": "",
    "Audience": "daryva-api"
  },
  "DevAuth": {
    "Enabled": false
  }
}
```

### 3. Test with Real Token

```bash
# 1. Get access token (example: Auth0)
TOKEN="eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."

# 2. Call API
curl -H "Authorization: Bearer $TOKEN" \
     -H "X-Org-Id: <your-org-guid>" \
     http://localhost:5000/api/houses
```

---

## Configuration: Development Without Auth (Testing)

### Option A: Empty Authority (No Validation)

Works for local testing without setup. Tokens are NOT validated.

```json
{
  "Jwt": {
    "Authority": "",
    "Audience": "daryva-api"
  }
}
```

⚠️ **WARNING:** This allows ANY Bearer token. Use for dev/test only.

Request with mock token:
```bash
curl -H "Authorization: Bearer mock-token" \
     http://localhost:5000/api/houses
```

### Option B: DevAuth Mode (Phase 5)

Inject a fake user automatically in development. See `DevAuthMiddleware.cs` (Phase 5).

---

## Multi-Tenancy: X-Org-Id Header

Once authenticated, the API determines which organization (org) the request operates on:

### 1. User Belongs to Multiple Orgs

**Must** specify X-Org-Id header:

```bash
curl -H "Authorization: Bearer <token>" \
     -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000" \
     http://localhost:5000/api/houses
```

Response if header missing:
```json
{
  "error": "Bad Request",
  "message": "You belong to multiple organizations. Specify X-Org-Id header.",
  "organizations": ["550e8400-e29b-41d4-a716-446655440000", "..."]
}
```

### 2. User Belongs to Single Org

X-Org-Id is auto-selected (optional header):

```bash
# With header (explicit):
curl -H "Authorization: Bearer <token>" \
     -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000" \
     http://localhost:5000/api/houses

# Without header (auto-selected):
curl -H "Authorization: Bearer <token>" \
     http://localhost:5000/api/houses
```

### 3. Invalid Org Selection

Response if user tries to access org they don't belong to:
```json
{
  "error": "Forbidden",
  "message": "You are not a member of the requested organization."
}
```

---

## Claims Extraction

The API extracts user identity from JWT claims in this order:

1. **NameIdentifier** claim (standard claim type, `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`)
2. **"sub"** claim (JWT subject, common in OpenID Connect)
3. Default: `"unknown-user"` (fallback, should indicate misconfiguration)

### Example JWT Payload

```json
{
  "iss": "https://your-tenant.auth0.com/",
  "sub": "google-oauth2|103548623197873...",
  "aud": "https://daryva-api.example.com",
  "iat": 1676890521,
  "exp": 1676977921,
  "email": "user@example.com",
  "email_verified": true
}
```

The `sub` (subject) claim becomes `TenantContext.UserId`.

---

## Error Responses

### 401 Unauthorized

Invalid/missing JWT:
```json
{
  "error": "Unauthorized",
  "message": "Invalid token or token expired."
}
```

### 403 Forbidden

User authenticated but not member of requested org:
```json
{
  "error": "Forbidden",
  "message": "You are not a member of the requested organization."
}
```

### 400 Bad Request

User belongs to multiple orgs but didn't specify X-Org-Id:
```json
{
  "error": "Bad Request",
  "message": "You belong to multiple organizations. Specify X-Org-Id header.",
  "organizations": ["550e8400-e29b-41d4-a716-446655440000"]
}
```

---

## Supported Auth Providers

The system is provider-agnostic. Any OpenID Connect provider works:

| Provider | Authority URL Pattern | Setup |
|----------|----------------------|-------|
| **Auth0** | `https://tenant.auth0.com/` | See [Auth0 Docs](https://auth0.com/docs) |
| **Azure AD B2C** | `https://tenant.b2clogin.com/tenant.onmicrosoft.com/b2c_1_...` | See [Azure AD B2C Docs](https://docs.microsoft.com/en-us/azure/active-directory-b2c/) |
| **Clerk** | `https://clerk.example.com/` | See [Clerk Docs](https://clerk.com/docs) |
| **Okta** | `https://dev-12345.okta.com/` | See [Okta Docs](https://developer.okta.com/docs) |
| **Google OAuth 2.0** | Requires custom OIDC proxy | Complex setup |

---

## Local Testing without Auth Provider

### 1. No Authority (Placeholder Tokens)

```json
{
  "Jwt": {
    "Authority": "",
    "Audience": "daryva-api"
  }
}
```

Any Bearer token is accepted (including `Bearer mock-token`).

### 2. DevAuth Middleware (Phase 5)

Automatically injects a fake user + org for all requests:

```json
{
  "DevAuth": {
    "Enabled": true,
    "UserId": "dev-user-1",
    "Email": "dev@local"
  }
}
```

---

## JWT Token Validation Details

When Authority is configured, tokens are validated for:

1. **Signature:** Public key from Authority's JWKS endpoint
2. **Issuer (iss):** Must match Authority
3. **Audience (aud):** Must match configured Audience
4. **Expiration (exp):** Token must not be expired
5. **Not-Before (nbf):** Token must not be used before activation time
6. **Issued-At (iat):** Sanity check on token age

**Clock Skew:** 0 seconds (strict validation; adjust if needed)

---

## Troubleshooting

### "Invalid token" Error

**Possible Causes:**
1. Token signature invalid → Verify Authority is correct
2. Audience mismatch → Check config matches your auth provider
3. Token expired → Get a fresh token
4. Provider JWKS endpoint unreachable → Check network, provider status

**Debug:**
```bash
curl -v -H "Authorization: Bearer <token>" \
     http://localhost:5000/api/houses
# Look for 401 response with details in logs
```

### "X-Org-Id header missing" Error

**Cause:** User belongs to multiple orgs but didn't specify which one.

**Fix:** Get user's orgs first via `GET /api/orgs`, then provide `X-Org-Id` header.

### "You are not a member" Error

**Cause:** `X-Org-Id` header specifies an org the user doesn't belong to.

**Fix:** Verify org GUID is correct; list user's orgs to confirm access.

---

## Next Steps

- Phase 4: Implement controllers (Create Org, List Orgs, CRUD Houses, etc.)
- Phase 5: Add DevAuth middleware for seamless local development
- Production: Replace Authority with your chosen provider and deploy
