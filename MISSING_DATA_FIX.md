# Missing Tenant Data - Comprehensive Fix Guide

## Problem Statement
After restoring the database backup with 11 tenants and 2 houses, the UI shows fewer tenants than expected. The data exists but isn't visible.

## Root Cause
**Organization Context Mismatch**: The application uses a multi-tenant architecture where:
- Data is imported with `OrganizationId = X` (the org requesting the import)
- But queries filter using `WHERE OrganizationId = CurrentOrgId` from the API request context
- If `X != CurrentOrgId`, the data is effectively invisible due to query filtering

## Why This Happens
1. When migration runs, it uses the org context from `_apiClient.CurrentOrgId`
2. Data gets imported to that organization in PostgreSQL
3. Later queries might use a different org context (different API header or dev user gets different org)
4. Global EF Core query filters enforce org isolation: `HasQueryFilter(t => t.OrganizationId == _tenantContext.CurrentOrgId)`
5. Result: Data exists but is filtered out by the WHERE clause

## Solution Overview
The fixes implement a **three-layer approach**:

### Layer 1: Diagnostic Endpoints (NEW)
**File**: `src/Daryva.Api/Controllers/DiagnosticController.cs`

Provides visibility into raw database contents without org filtering:
- `GET /api/diagnostic/data-counts` - Shows raw counts grouped by org
- `GET /api/diagnostic/org-members` - Shows user-org relationships

This lets you answer:
- "How many organizations exist?"
- "Which org has the data?"
- "Which org is the user a member of?"

### Layer 2: Organization Sync (NEW)
**File**: `src/Daryva.Api/Services/Seed/OrganizationSyncService.cs`

Ensures the dev user is a member of all organizations with data:
- Runs on startup in DevAuth mode
- Finds all orgs that have tenants/houses
- Adds the dev user to those orgs if not already a member
- Prevents the case where data exists but user can't access it

This ensures: **User can access all data organizations**

### Layer 3: Explicit Organization Logging (MODIFIED)
**Files Modified**:
- `src/Daryva.UI/MVVM/ViewModels/MigrationViewModel.cs` - Added debug logging
- `src/Daryva.Api/Services/BulkImportService.cs` - Logs which org data goes to
- `src/Daryva.Api/Controllers/TenantsController.cs` - Logs which org queries use

This ensures: **You can see which org data goes to and which org queries use**

### Layer 4: Helper Extension Methods (NEW)
**File**: `src/Daryva.UI/Services/Api/OrganizationApiServiceExtensions.cs`

Makes it easy to manage org context:
```csharp
await apiClient.SetApiContextOrgAsync(organizationService);
```

This ensures: **Org context is set consistently**

## How to Use the Fix

### Step 1: Verify the Data Exists
```bash
# Start the API server
cd src/Daryva.Api
dotnet run

# In another terminal, call the diagnostic endpoint
curl http://localhost:5000/api/diagnostic/data-counts
```

This shows:
- Raw count of all tenants across all orgs (should be 11)
- Organizations that have data
- Which org each set of data belongs to

### Step 2: Check User's Organization Access
```bash
curl http://localhost:5000/api/diagnostic/org-members
```

This shows:
- Which organizations exist
- Which user is a member of which org

### Step 3: Run the Migration/Queries
On startup, the system now:
1. Seeds data if needed
2. **Syncs the dev user to all data organizations**
3. Sets proper logging so you can trace what's happening

When you click "Migrate" in the UI:
1. Log output shows: "Auto-selected organization: {id} ({name})"
2. Migration logs: "Starting bulk import for organization {OrgId}: ..."
3. Import logs: "Imported X houses, Y tenants" (counts should match SQLite)

### Step 4: Verify Data is Visible
1. Click "Migration" tab
2. Click "Start Migration"
3. Wait for completion
4. Navigate to "Tenants" tab
5. Should see all 11 tenants

If you still don't see data:
1. Run `GET /api/diagnostic/data-counts`
2. Check if Total tenants count matches SQLite count
3. Look at console logs to see which org data is in
4. Ensure API client set same org context via X-Org-Id header

## Code Changes Detail

### DiagnosticController.cs (NEW)
```csharp
public class DiagnosticController : ControllerBase
{
    [HttpGet("data-counts")]
    public async Task<ActionResult> GetDataCounts()
    {
        // Returns raw counts using .IgnoreQueryFilters()
        // Shows data organization membership
    }

    [HttpGet("org-members")]
    public async Task<ActionResult> GetOrgMembers()
    {
        // Shows which users belong to which orgs
    }
}
```

### OrganizationSyncService.cs (NEW)
```csharp
public class OrganizationSyncService : IOrganizationSyncService
{
    public async Task SyncUserOrgMembershipsAsync(string userId)
    {
        // Finds orgs with data
        // Adds user to orgs they're not a member of
        // Runs on startup during dev mode
    }
}
```

### MigrationViewModel.cs (MODIFIED)
Added debug logging:
```csharp
System.Diagnostics.Debug.WriteLine(
    $"[MigrationViewModel] Auto-selected organization: {orgId} ({orgs[0].Name})");
```

### BulkImportService.cs (MODIFIED)
Added info logging:
```csharp
_logger.LogInformation(
    "Starting bulk import for organization {OrgId}: {Houses} houses, {Tenants} tenants...",
    organizationId,
    request.Houses.Count,
    request.Tenants.Count);
```

### TenantsController.cs (MODIFIED)
Added request/response logging:
```csharp
_logger.LogInformation("Retrieved {TenantCount} tenants for organization {OrgId}", 
    tenants.Count(), _tenantContext.CurrentOrgId);
```

### Program.cs (MODIFIED)
1. Registered `IOrganizationSyncService`
2. Added startup call to sync org memberships:
```csharp
var orgSyncService = scope.ServiceProvider.GetRequiredService<IOrganizationSyncService>();
var devUserId = app.Configuration.GetValue<string>("DevAuth:UserId") ?? "dev-user-1";
await orgSyncService.SyncUserOrgMembershipsAsync(devUserId);
```

## Expected Behavior After Fix

1. **On API Startup**:
   - Database migrations run
   - Sample data seeded if needed
   - Dev user synced to all organizations with data
   - Log: "Organization sync completed for user dev-user-1"
   - Log: "Added user dev-user-1 to organization {id} ({name})" (for each data org)

2. **When Running Migration**:
   - Log: "Auto-selected organization: {guid} ({name})"
   - Log: "Starting bulk import for organization {guid}: 11 tenants..."
   - Log: "Imported 11 tenants" (matches SQLite count)

3. **When Querying Tenants**:
   - Log: "Retrieved 11 tenants for organization {guid}"
   - UI displays all 11 tenants

4. **Diagnostic Endpoints**:
   - `/api/diagnostic/data-counts` shows tenants: 11
   - `/api/diagnostic/org-members` shows dev-user-1 in all data orgs

## Testing Checklist

- [ ] API builds successfully
- [ ] UI builds successfully  
- [ ] API starts without errors
- [ ] `GET /api/diagnostic/data-counts` returns 11 tenants total
- [ ] `GET /api/diagnostic/org-members` shows dev user in data orgs
- [ ] Click Migration tab, start migration
- [ ] Migration completes with "11 tenants imported"
- [ ] Click Tenants tab
- [ ] All 11 tenants are visible

## Troubleshooting

**Issue**: Still no tenants showing
**Solution**: 
1. Check diagnostic endpoint: `curl http://localhost:5000/api/diagnostic/data-counts`
2. Does it show 11 tenants total? If no, data wasn't actually imported
3. If yes, check which org has the data
4. Ensure API client is using correct org ID

**Issue**: Diagnostic endpoints return 403 Forbidden
**Solution**: Check that DevAuth is enabled in appsettings.json

**Issue**: "Organization context not set" error
**Solution**: Make sure X-Org-Id header is set, or user is a member of exactly one org

## Architecture Notes

The organization filtering is **intentional and correct** - it provides data isolation between organizations. The issue was just:
- Data imported to Org A
- But queries used Org B
- OrganizationSyncService fixes this by adding user to Org A

## Files Modified This Session

1. `src/Daryva.Api/Controllers/DiagnosticController.cs` - NEW
2. `src/Daryva.Api/Services/Seed/OrganizationSyncService.cs` - NEW
3. `src/Daryva.UI/Services/Api/OrganizationApiServiceExtensions.cs` - NEW
4. `src/Daryva.UI/MVVM/ViewModels/MigrationViewModel.cs` - MODIFIED (logging)
5. `src/Daryva.Api/Services/BulkImportService.cs` - MODIFIED (logging)
6. `src/Daryva.Api/Controllers/TenantsController.cs` - MODIFIED (logging)
7. `src/Daryva.Api/Program.cs` - MODIFIED (register service + startup call)

## Next Steps

1. Build the solution
2. Run the API server
3. Check diagnostic endpoints
4. Run migration
5. Verify all 11 tenants appear
6. If still not working, check logs for organization context mismatches
