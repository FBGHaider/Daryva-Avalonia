# System Architecture Diagram

## Data Flow with Organization Context

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        DARYVA MULTI-TENANT SYSTEM                       │
└─────────────────────────────────────────────────────────────────────────┘

┌──────────────────────┐
│   DESKTOP UI         │
│  (Avalonia MVVM)     │
│                      │
│ • MigrationViewModel │  ◄─── Sets orgId = CurrentOrgId
│ • TenantsViewModel   │
│ • TenantTab          │
└────────┬─────────────┘
         │
         │ GET /api/tenants (X-Org-Id header)
         │
         ▼
┌──────────────────────────────────────────┐
│        ASP.NET CORE 8.0 API SERVER       │
│         (localhost:5000)                 │
├──────────────────────────────────────────┤
│                                          │
│  TenantContextMiddleware                 │ ◄─── Reads X-Org-Id header
│  ├─ Extracts orgId from header           │      Sets _tenantContext
│  ├─ Validates user membership            │      (used by global filters)
│  └─ Sets CurrentOrgId                    │
│                                          │
│  TenantsController                       │ ◄─── Checks CurrentOrgId
│  └─ GET /api/tenants                     │
│                                          │
│  TenantService                           │ ◄─── Has global filter
│  └─ GetAllTenantsAsync()                 │
│     WHERE OrganizationId = CurrentOrgId  │
│                                          │
│  ┌─ NEW ─────────────────────────────┐   │
│  │ DiagnosticController              │   │  ◄─── FOR DEBUGGING
│  │ • /api/diagnostic/data-counts     │   │      Shows raw data
│  │ • /api/diagnostic/org-members     │   │      Ignores filters
│  └───────────────────────────────────┘   │
│                                          │
│  ┌─ NEW ─────────────────────────────┐   │
│  │ OrganizationSyncService           │   │  ◄─── AUTOMATIC FIX
│  │ (runs on startup)                 │   │      Adds user to data orgs
│  │ • Finds orgs with tenants         │   │
│  │ • Adds user to org memberships    │   │
│  └───────────────────────────────────┘   │
│                                          │
└────────────┬─────────────────────────────┘
             │
             │ EF Core with Global Query Filters
             │
             ▼
┌──────────────────────────────────────────┐
│      POSTGRESQL DATABASE                 │
│      (daryva)                            │
├──────────────────────────────────────────┤
│                                          │
│  Table: Tenant                           │
│  ├─ Id (GUID)                            │
│  ├─ FullName                             │
│  ├─ Email                                │
│  ├─ OrganizationId (MULTI-TENANT KEY)   │
│  └─ ... (other columns)                  │
│                                          │
│  Global Filter Query:                    │
│  WHERE OrganizationId = @CurrentOrgId    │
│                                          │
│  Table: Organization                     │
│  ├─ Id (GUID)                            │
│  └─ Name                                 │
│                                          │
│  Table: OrganizationMember               │
│  ├─ UserId                               │
│  ├─ OrganizationId                       │
│  └─ Role                                 │
│                                          │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│      SQLITE DATABASE (Local)             │
│      (backup for import)                 │
├──────────────────────────────────────────┤
│                                          │
│  Table: Tenant (11 rows)                 │
│  Table: House (2 rows)                   │
│  Table: Tenancy                          │
│  Table: Expense                          │
│  Table: Document                         │
│                                          │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│      MIGRATION PROCESS                   │
├──────────────────────────────────────────┤
│                                          │
│  1. SQLite → Read Entities               │
│     (11 tenants, 2 houses, ...)         │
│                                          │
│  2. → Build Import Request               │
│     (ImportTenantDto, ImportHouseDto...) │
│                                          │
│  3. → POST /api/import                   │
│     With X-Org-Id header                │
│                                          │
│  4. → BulkImportService                  │
│     Creates entities with:              │
│     OrganizationId = _tenantContext.Cur  │
│                                          │
│  5. → PostgreSQL                         │
│     Data now visible only to users       │
│     in that organization!               │
│                                          │
└──────────────────────────────────────────┘
```

## Problem Scenario (BEFORE FIX)

```
┌──────────────────────────────────────────────────────────┐
│ IMPORT PHASE                                             │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ _tenantContext.CurrentOrgId = {guid-of-Org-A}           │
│         ↓                                                │
│ Data imported with OrganizationId = Org-A               │
│         ↓                                                │
│ PostgreSQL:                                              │
│   INSERT INTO Tenant (..., OrganizationId=Org-A)        │
│                                                          │
└──────────────────────────────────────────────────────────┘
                          │
                          │ [CONTEXT CHANGES]
                          ▼
┌──────────────────────────────────────────────────────────┐
│ QUERY PHASE (PROBLEM!)                                   │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ _tenantContext.CurrentOrgId = {guid-of-Org-B}  ⚠️     │
│         ↓                                                │
│ Query:                                                   │
│   SELECT * FROM Tenant                                   │
│   WHERE OrganizationId = Org-B     ← WRONG ORG!        │
│         ↓                                                │
│ Result: 0 TENANTS (Org-A data is hidden!)              │
│                                                          │
└──────────────────────────────────────────────────────────┘

⚠️ DATA IS IN THE DATABASE BUT INVISIBLE BECAUSE:
   Organization filtering blocks access to data from Org-A
   when querying as user in Org-B
```

## Solution Scenario (AFTER FIX)

```
┌──────────────────────────────────────────────────────────┐
│ STARTUP PHASE (NEW!)                                     │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ OrganizationSyncService:                                │
│   1. Find orgs with data: [Org-A (has tenants)]        │
│   2. Check user memberships: [Org-A? NO]               │
│   3. Add user to Org-A:                                │
│      INSERT INTO OrganizationMembers (user, Org-A)    │
│                                                          │
│   ✅ User is now member of Org-A                       │
│                                                          │
└──────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│ IMPORT PHASE                                             │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ _tenantContext.CurrentOrgId = {guid-of-Org-A}           │
│         ↓                                                │
│ Data imported with OrganizationId = Org-A               │
│         ↓                                                │
│ PostgreSQL:                                              │
│   INSERT INTO Tenant (..., OrganizationId=Org-A)        │
│                                                          │
└──────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│ QUERY PHASE (FIXED!)                                     │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ _tenantContext.CurrentOrgId = {guid-of-Org-A}  ✅     │
│         ↓                                                │
│ Query:                                                   │
│   SELECT * FROM Tenant                                   │
│   WHERE OrganizationId = Org-A     ← CORRECT ORG!      │
│         ↓                                                │
│ Result: 11 TENANTS ✅                                   │
│                                                          │
│ UI displays all tenants!                                │
│                                                          │
└──────────────────────────────────────────────────────────┘

✅ DATA IS VISIBLE BECAUSE:
   1. Data was imported to Org-A
   2. User is now a member of Org-A
   3. Org context correctly set to Org-A
   4. Query filter allows access to Org-A data
```

## Component Interaction Diagram

```
┌────────────────────────┐
│  MigrationViewModel    │
│  [MODIFIED]            │
│                        │
│  1. Get user orgs      │──────┐
│  2. Select org         │      │
│  3. Set X-Org-Id       │      │ Debug logging
│  4. Call migration API │      │ shows which org
└────────────────────────┘      │
                                │
                 ┌──────────────┘
                 │
                 ▼
┌────────────────────────┐
│  ApiClient             │
│                        │
│  Sets header:          │
│  X-Org-Id: {guid}      │
└────────────┬───────────┘
             │
             ▼
┌────────────────────────┐
│  HTTP Request          │
│  POST /api/import      │
│  X-Org-Id: {guid}      │
└────────────┬───────────┘
             │
             ▼
┌─────────────────────────────────┐
│  TenantContextMiddleware        │
│  [MODIFIED]                     │
│                                 │
│  1. Read X-Org-Id header        │
│  2. Validate membership         │
│  3. Set CurrentOrgId            │
│  4. Debug logging               │
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│  ImportController               │
│  → BulkImportService            │
│  [MODIFIED]                     │
│                                 │
│  1. Receive org context         │
│  2. Import data                 │
│  3. Log stats with org ID       │
│  4. Return success              │
└──────────────┬──────────────────┘
               │
               ▼
        ┌──────────────┐
        │  PostgreSQL  │
        │  Database    │
        └──────────────┘
```

## Diagnostic Endpoints (NEW)

```
┌─────────────────────────────────────────────────┐
│  GET /api/diagnostic/data-counts                │
│  [NEW - DiagnosticController]                   │
├─────────────────────────────────────────────────┤
│                                                 │
│  Uses: .IgnoreQueryFilters()                   │
│  Purpose: See raw data without org filtering    │
│                                                 │
│  Returns:                                       │
│  {                                              │
│    counts: {                                    │
│      tenants: 11,     ← Total across all orgs  │
│      houses: 2                                  │
│    },                                           │
│    tenantSummary: [                             │
│      {                                          │
│        organizationId: "org-a-guid",            │
│        count: 11,                               │
│        names: ["Tenant1", "Tenant2", ...]      │
│      }                                          │
│    ]                                            │
│  }                                              │
│                                                 │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  GET /api/diagnostic/org-members                │
│  [NEW - DiagnosticController]                   │
├─────────────────────────────────────────────────┤
│                                                 │
│  Purpose: See user-org relationships           │
│                                                 │
│  Returns:                                       │
│  {                                              │
│    members: [                                   │
│      {                                          │
│        userId: "dev-user-1",                    │
│        organizationId: "org-a-guid",            │
│        organizationName: "Default Organization",│
│        role: "owner"                            │
│      }                                          │
│    ]                                            │
│  }                                              │
│                                                 │
└─────────────────────────────────────────────────┘
```

This shows how the multi-tenant organization filtering works and how the fix ensures users have access to all data organizations.
