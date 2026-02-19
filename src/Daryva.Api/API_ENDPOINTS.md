# Phase 4: API Endpoints & Controllers

## Overview

Daryva API now provides complete CRUD endpoints for organizations and houses with proper:
- ✅ Multi-tenant isolation (X-Org-Id header)
- ✅ Request validation (DataAnnotations)
- ✅ Proper HTTP status codes
- ✅ Error handling
- ✅ Async/await throughout
- ✅ Cancellation token support
- ✅ Comprehensive logging

## Architecture

```
Request
  ↓
[Authentication] (JWT Bearer)
  ↓
[TenantContextMiddleware] (Set CurrentOrgId)
  ↓
[Controller] (Route + Authorization)
  ↓
[Service Layer] (Business logic)
  ↓
[AppDbContext] (Global query filters enforce OrgId isolation)
  ↓
[PostgreSQL]
```

### Key Files

- **Controllers:**
  - `Controllers/OrgsController.cs` — Organization management
  - `Controllers/HousesController.cs` — Property management

- **Services:**
  - `Services/OrganizationService.cs` — Business logic for orgs
  - `Services/HouseService.cs` — Business logic for houses

- **DTOs:**
  - `Dtos/OrganizationDtos.cs` — Org request/response models
  - `Dtos/OrganizationMemberDtos.cs` — Member request/response models
  - `Dtos/HouseDtos.cs` — House request/response models

---

## API Endpoints

All endpoints except `/health` require:
- **Authorization:** `Authorization: Bearer <jwt_token>` header
- **Organization Context:** `X-Org-Id` header (if user belongs to multiple orgs)

---

### Organizations (`/api/orgs`)

#### 1. Create Organization

**POST** `/api/orgs`

Create a new organization. Current user automatically becomes the **Owner**.

**Request:**
```json
{
  "name": "John's Property Management"
}
```

**Response:** `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "John's Property Management",
  "createdAt": "2026-02-19T02:15:00Z",
  "currentUserRole": "Owner"
}
```

**Error Responses:**
- `400 Bad Request` — Invalid payload
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
curl -X POST http://localhost:5000/api/orgs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "John'\''s Property Mgmt"}'
```

---

#### 2. List Organizations

**GET** `/api/orgs`

Get all organizations the user belongs to.

**Response:** `200 OK`
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "John's Property Management",
    "createdAt": "2026-02-19T02:15:00Z",
    "currentUserRole": "Owner"
  },
  {
    "id": "660e8400-e29b-41d4-a716-446655440000",
    "name": "Jane's Rentals",
    "createdAt": "2026-02-18T10:30:00Z",
    "currentUserRole": "Member"
  }
]
```

**cURL Example:**
```bash
curl -X GET http://localhost:5000/api/orgs \
  -H "Authorization: Bearer $TOKEN"
```

---

#### 3. Get Organization

**GET** `/api/orgs/{orgId}`

Get details of a specific organization (if user is member).

**Response:** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "John's Property Management",
  "createdAt": "2026-02-19T02:15:00Z",
  "currentUserRole": "Owner"
}
```

**Error Responses:**
- `404 Not Found` — Org doesn't exist or user is not a member
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
curl -X GET http://localhost:5000/api/orgs/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer $TOKEN"
```

---

#### 4. Add Member to Organization

**POST** `/api/orgs/{orgId}/members`

Add a user to an organization by email. User becomes the specified role.

**Request:**
```json
{
  "email": "newmember@example.com",
  "role": "Member"
}
```

**Valid Roles:**
- `"Owner"` — Full control
- `"Admin"` — Administrative access
- `"Member"` — Standard access
- `"ReadOnly"` — Read-only access

**Response:** `201 Created`
```json
{
  "id": "770e8400-e29b-41d4-a716-446655440000",
  "userId": "auth0|550e8400e29b",
  "email": "newmember@example.com",
  "role": "Member",
  "joinedAt": "2026-02-19T02:16:00Z"
}
```

**Note:** User ID is a placeholder until the user logs in and is matched against the auth provider.

**Error Responses:**
- `400 Bad Request` — Invalid role or email already a member
- `403 Forbidden` — User not a member of organization
- `404 Not Found` — Org doesn't exist
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
curl -X POST http://localhost:5000/api/orgs/550e8400-e29b-41d4-a716-446655440000/members \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"email": "newmember@example.com", "role": "Member"}'
```

---

#### 5. List Organization Members

**GET** `/api/orgs/{orgId}/members`

Get all members of an organization (if user is member).

**Response:** `200 OK`
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "userId": "google-oauth2|103548623197873",
    "email": "owner@example.com",
    "role": "Owner",
    "joinedAt": "2026-02-19T02:15:00Z"
  },
  {
    "id": "660e8400-e29b-41d4-a716-446655440000",
    "userId": "placeholder-user-id",
    "email": "member@example.com",
    "role": "Member",
    "joinedAt": "2026-02-19T02:16:00Z"
  }
]
```

**cURL Example:**
```bash
curl -X GET http://localhost:5000/api/orgs/550e8400-e29b-41d4-a716-446655440000/members \
  -H "Authorization: Bearer $TOKEN"
```

---

### Houses (`/api/houses`)

#### 1. List Houses

**GET** `/api/houses`

Get all houses for the current organization.

**Required:** X-Org-Id header (if user belongs to multiple orgs)

**Response:** `200 OK`
```json
[
  {
    "id": "880e8400-e29b-41d4-a716-446655440000",
    "organizationId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Main St Apartment A",
    "addressLine1": "123 Main Street",
    "addressLine2": "Apt 1A",
    "city": "New York",
    "postcode": "10001",
    "createdAt": "2026-02-19T02:17:00Z"
  }
]
```

**Error Responses:**
- `400 Bad Request` — Org context not set (multiple orgs, missing header)
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
# Auto-selected (user has 1 org):
curl -X GET http://localhost:5000/api/houses \
  -H "Authorization: Bearer $TOKEN"

# Explicit org (user has multiple orgs):
curl -X GET http://localhost:5000/api/houses \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000"
```

---

#### 2. Get House

**GET** `/api/houses/{houseId}`

Get details of a specific house.

**Response:** `200 OK`
```json
{
  "id": "880e8400-e29b-41d4-a716-446655440000",
  "organizationId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Main St Apartment A",
  "addressLine1": "123 Main Street",
  "addressLine2": "Apt 1A",
  "city": "New York",
  "postcode": "10001",
  "createdAt": "2026-02-19T02:17:00Z"
}
```

**Error Responses:**
- `400 Bad Request` — Org context not set
- `404 Not Found` — House doesn't exist or belongs to different org
- `401 Unauthorized` — Missing/invalid token

---

#### 3. Create House

**POST** `/api/houses`

Create a new house. **OrganizationId is set server-side; client input ignored.**

**Request:**
```json
{
  "name": "Main St Apartment A",
  "addressLine1": "123 Main Street",
  "addressLine2": "Apt 1A",
  "city": "New York",
  "postcode": "10001"
}
```

**Response:** `201 Created`
```json
{
  "id": "880e8400-e29b-41d4-a716-446655440000",
  "organizationId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Main St Apartment A",
  "addressLine1": "123 Main Street",
  "addressLine2": "Apt 1A",
  "city": "New York",
  "postcode": "10001",
  "createdAt": "2026-02-19T02:17:00Z"
}
```

**Error Responses:**
- `400 Bad Request` — Invalid payload or org context not set
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
curl -X POST http://localhost:5000/api/houses \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Main St Apartment A",
    "addressLine1": "123 Main Street",
    "city": "New York",
    "postcode": "10001"
  }'
```

---

#### 4. Update House

**PUT** `/api/houses/{houseId}`

Update an existing house. Only non-null fields are updated.

**Request:**
```json
{
  "name": "Main St Apartment A - Updated",
  "city": "NYC"
}
```

**Response:** `200 OK`
```json
{
  "id": "880e8400-e29b-41d4-a716-446655440000",
  "organizationId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Main St Apartment A - Updated",
  "addressLine1": "123 Main Street",
  "addressLine2": "Apt 1A",
  "city": "NYC",
  "postcode": "10001",
  "createdAt": "2026-02-19T02:17:00Z"
}
```

**Error Responses:**
- `400 Bad Request` — Invalid payload or org context not set
- `404 Not Found` — House doesn't exist
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
curl -X PUT http://localhost:5000/api/houses/880e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{"city": "NYC"}'
```

---

#### 5. Delete House

**DELETE** `/api/houses/{houseId}`

Delete a house.

**Response:** `204 No Content`

**Error Responses:**
- `400 Bad Request` — Org context not set
- `404 Not Found` — House doesn't exist
- `401 Unauthorized` — Missing/invalid token

**cURL Example:**
```bash
curl -X DELETE http://localhost:5000/api/houses/880e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Org-Id: 550e8400-e29b-41d4-a716-446655440000"
```

---

## Testing Locally

### Prerequisite: Start API

```bash
cd src/Daryva.Api
dotnet run
```

API will start at `http://localhost:5000` (HTTP) or `https://localhost:5001` (HTTPS).

### Option 1: Using cURL (No Auth Required in Dev)

Since JWT Authority is empty in Development, any Bearer token is accepted.

```bash
# 1. Create org
ORG_ID=$(curl -s -X POST http://localhost:5000/api/orgs \
  -H "Authorization: Bearer dummy-token" \
  -H "Content-Type: application/json" \
  -d '{"name": "Test Org"}' | jq -r '.id')

echo "Created org: $ORG_ID"

# 2. Create house
curl -X POST http://localhost:5000/api/houses \
  -H "Authorization: Bearer dummy-token" \
  -H "X-Org-Id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test House",
    "addressLine1": "123 Test St",
    "city": "Test City",
    "postcode": "12345"
  }' | jq

# 3. List houses
curl -X GET http://localhost:5000/api/houses \
  -H "Authorization: Bearer dummy-token" \
  -H "X-Org-Id: $ORG_ID" | jq
```

### Option 2: Using Swagger UI

Open browser to: `http://localhost:5000/swagger`

1. Click "Authorize" (top right)
2. Enter: `Bearer dummy-token` (in Development, any token works)
3. Try endpoints interactively

---

## Multi-Tenancy Validation Flow

**Example:** User belongs to 2 orgs, tries to create house without X-Org-Id header

```
Request:
  POST /api/houses
  Authorization: Bearer <token>
  (no X-Org-Id header)

TenantContextMiddleware:
  1. Query: SELECT OrganizationId FROM OrganizationMembers WHERE UserId = ?
  2. Result: 2 orgs found
  3. Response: 400 Bad Request
     {
       "error": "Bad Request",
       "message": "You belong to multiple organizations. Specify X-Org-Id header.",
       "organizations": ["org-id-1", "org-id-2"]
     }
```

**Fixed request:**

```
Request:
  POST /api/houses
  Authorization: Bearer <token>
  X-Org-Id: org-id-1

TenantContextMiddleware:
  1. Query: SELECT * FROM OrganizationMembers WHERE UserId = ? AND OrganizationId = org-id-1
  2. Result: Found (user is member)
  3. Set CurrentOrgId = org-id-1
  4. Continue to controller

Controller/Service:
  - Create house with OrganizationId = CurrentOrgId (server-side)
  - Save to DB
  - Return created house
```

---

## Error Handling

### Standard HTTP Status Codes

| Status | Meaning | Example |
|--------|---------|---------|
| 200 OK | Success | GET /api/houses |
| 201 Created | Resource created | POST /api/houses |
| 204 No Content | Success, no body | DELETE /api/houses/{id} |
| 400 Bad Request | Invalid input or org context | Missing X-Org-Id header, invalid JSON |
| 401 Unauthorized | Auth missing/invalid | Missing Bearer token, token expired |
| 403 Forbidden | Authenticated but not allowed | User not member of org |
| 404 Not Found | Resource not found | House doesn't exist |

### Response Format

**Success (2xx):** Returns resource or resource list

```json
{
  "id": "...",
  "name": "...",
  ...
}
```

**Error (4xx):** Returns error object

```json
{
  "error": "Bad Request|Unauthorized|Forbidden|Not Found",
  "message": "Human-readable error description"
}
```

---

## Next Steps

- Phase 5: Implement DevAuth middleware for seamless local development
- Add integration tests for all endpoints
- Add pagination/filtering for list endpoints
- Add audit logging (who changed what, when)
- Deploy to production with real JWT provider

---

## Files Modified/Created

✅ `Controllers/OrgsController.cs` — 5 org endpoints
✅ `Controllers/HousesController.cs` — 5 house CRUD endpoints
✅ `Services/OrganizationService.cs` — 5 org operations
✅ `Services/HouseService.cs` — 5 house operations
✅ `Dtos/OrganizationDtos.cs` — Org request/response models
✅ `Dtos/OrganizationMemberDtos.cs` — Member request/response models
✅ `Dtos/HouseDtos.cs` — House request/response models
✅ `Program.cs` — Register services in DI container

**Build Status:** ✅ All projects compile successfully (0 errors)
