-- =============================================
-- End Tenancies for Archived Tenants
-- This script ends all active tenancies for tenants that are archived
-- =============================================

USE [LandLordBuddyDB]
GO

PRINT 'Ending tenancies for archived tenants...';
GO

-- End all active tenancies for archived tenants
UPDATE t
SET t.Status = 'Ended',
    t.MoveOutDate = GETUTCDATE()
FROM Tenancy t
INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
WHERE t.Status = 'Active' AND tn.IsArchived = 1;

PRINT 'Tenancies for archived tenants ended.';
GO

-- Show summary
SELECT 
    COUNT(*) AS EndedTenancies,
    'Total tenancies ended for archived tenants' AS Description
FROM Tenancy t
INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
WHERE t.Status = 'Ended' AND tn.IsArchived = 1;

-- Show remaining active tenancies
SELECT 
    COUNT(*) AS ActiveTenancies,
    'Total active tenancies (non-archived tenants only)' AS Description
FROM Tenancy t
INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
WHERE t.Status = 'Active' AND tn.IsArchived = 0;

PRINT 'Script completed!';
GO
