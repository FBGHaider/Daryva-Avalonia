# Data Missing Issue - Root Cause Analysis & Solution

## Problem Summary
User reported: "nope it's still missing many data" - tenants that should be visible in the UI are not appearing after migration.

## Root Cause Analysis

### Architecture Overview
The application uses a **multi-tenant PostgreSQL backend** with **organization-based data isolation**:

1. **CLI → SQLite**: User clicks "Migrate" button in UI
2. **SQLite → API**: Migration service reads data from local SQLite, sends to API endpoint `POST /api/import`
3. **API → PostgreSQL**: BulkImportService receives data, imports with `OrganizationId = _tenantContext.CurrentOrgId`
4. **Query Filtering**: All queries use global EF Core query filters that filter by `OrganizationId == _tenantContext.CurrentOrgId`

### The Problem: Organization Context Mismatch

**Data is imported with:**
```csharp
// BulkImportService.cs, line 119
var house = new House
{
    Id = Guid.NewGuid(),
    OrganizationId = organizationId,  // <-- Set from _tenantContext.CurrentOrgId
    ...
};
```

**But queries might use a DIFFERENT organization:**
```csharp
// TenantService.cs, line 70
public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false, ...)
{
    var query = _dbContext.Tenants.AsNoTracking();
    // Global query filter: WHERE OrganizationId == _tenantContext.CurrentOrgId
    // If this is a DIFFERENT org than import used, NO DATA IS RETURNED!
}
```

### How to Verify the Problem

The following diagnostic endpoint was created:
- **GET /api/diagnostic/data-counts** - Shows raw data (unfiltered by org)
- **GET /api/diagnostic/org-members** - Shows organization memberships

These bypass global query filters using `.IgnoreQueryFilters()`.

### Suspected Scenario
1. Data was imported under Organization A (created during first migration)
2. User/API client context is now using Organization B (or different org)
3. Queries filter by Organization B, so Organization A's data is invisible

## Solution Strategy

### Step 1: Start Services & Check Data
```powershell
# Start PostgreSQL
docker-compose up -d postgres

# Wait 10 seconds for database to be ready
Start-Sleep -Seconds 10

# Build API
dotnet build src/Daryva.Api

# Run API server (in background)
dotnet run --project src/Daryva.Api/Daryva.Api.csproj &
```

### Step 2: Verify Data with Diagnostic Endpoints
```bash
# Check raw data counts (no org filtering)
curl http://localhost:5000/api/diagnostic/data-counts

# Check organization memberships
curl http://localhost:5000/api/diagnostic/org-members
```

This will show:
- How many organizations exist
- Which organization the data belongs to
- Which organization the current user is a member of

### Step 3: Ensure Organization Consistency
Before running migration again:

1. **Ensure CurrentOrgId is set correctly**
   - Check `MigrationViewModel.cs` line 130-145
   - It reads `_apiClient.CurrentOrgId` or auto-selects first org
   - This IS the correct organization to use

2. **When calling API, ensure X-Org-Id header matches**
   - `TenantApiService.cs` calls `GET api/tenants?includeArchived=true`
   - `ApiClient.cs` sets the `X-Org-Id` header automatically
   - This header is used by `TenantContextMiddleware` to set `CurrentOrgId`

3. **After migration, verify org context persists**
   - After importing, the `_apiClient.CurrentOrgId` should remain the same
   - All subsequent queries should use the same org context

### Step 4: Re-run Migration with Corrected Context
```
1. Open UI
2. Click on Migration tab
3. Click "Start Migration"
4. Wait for completion
5. Check Tenants tab to verify all 11 tenants appear
```

## Code References

### Migration Flow
- **MigrationViewModel.cs:L160** - Calls `_migrationService.MigrateAllDataAsync(orgId.Value, progress)`
- **SqliteToApiMigrationService.cs:L254** - Makes HTTP POST to `/api/import`
- **ImportController.cs:L47** - Receives import request, validates org context
- **BulkImportService.cs:L104-119** - Creates entities with `OrganizationId = organizationId`

### Query Flow
- **TenantsController.cs:L41** - API endpoint `GET /api/tenants`
- **TenantService.cs:L70** - Queries with global filter
- **AppDbContext.cs:L214** - Global query filter: `.HasQueryFilter(t => t.OrganizationId == _tenantContext.CurrentOrgId)`

### Organization Context Handling
- **ApiClient.cs:L30-33** - Sets `X-Org-Id` header when calling API
- **TenantContextMiddleware.cs:L30-99** - Reads `X-Org-Id` header, validates membership, sets context

## Additional Notes

### Multi-Tenant Isolation Works Correctly
The organization filtering is **intentional** - it provides data isolation between organizations. The problem is just that:
- Data might have been imported to one org
- But we're querying a different org
- So nothing appears

### How to Verify Fix Worked
1. Run diagnostic endpoint: `GET /api/diagnostic/data-counts`
2. Should see:
   - `"tenants": 11` (total across all orgs)
   - Data grouped by organization showing which org has the data
3. Then ensure API client is using correct org before querying
4. When querying with correct org, all 11 tenants should appear

### Files Modified in This Session
- **DiagnosticController.cs** (NEW) - Diagnostic endpoints
- **MigrationViewModel.cs** - Organization context logic
- **BulkImportService.cs** - Sets OrganizationId during import
- **TenantService.cs** - Uses global query filters for isolation
- **ApiClient.cs** - Manages org context headers
- **TenantContextMiddleware.cs** - Reads and validates org from headers

## Next Steps
1. Verify PostgreSQL and API server are running
2. Call diagnostic endpoints to see raw data
3. Identify which org has the data
4. Ensure API client sets X-Org-Id header to correct org
5. Re-run migration if needed with correct org context
