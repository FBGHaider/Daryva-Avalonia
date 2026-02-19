-- =============================================
-- Cleanup Duplicate and Old Tenants
-- This script identifies and archives duplicate tenants
-- keeping only the most recent one based on CreatedAt
-- =============================================

USE [DaryvaDB]
GO

PRINT 'Starting tenant cleanup...';
GO

-- Step 1: Identify duplicate tenants by Email (most reliable identifier)
-- Keep the most recent tenant (highest TenantId or latest CreatedAt) and archive others
PRINT 'Step 1: Archiving duplicate tenants by Email...';
GO

WITH DuplicateTenants AS (
    SELECT 
        TenantId,
        Email,
        FullName,
        CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY Email 
            ORDER BY CreatedAt DESC, TenantId DESC
        ) AS RowNum
    FROM Tenant
    WHERE IsArchived = 0
)
UPDATE t
SET t.IsArchived = 1
FROM Tenant t
INNER JOIN DuplicateTenants dt ON t.TenantId = dt.TenantId
WHERE dt.RowNum > 1; -- Keep only the first (most recent) one

PRINT 'Duplicate tenants by Email archived.';
GO

-- Step 2: Identify duplicate tenants by FullName + PhoneNumber
-- (in case same person was added with different emails)
PRINT 'Step 2: Archiving duplicate tenants by Name + Phone...';
GO

WITH DuplicateTenantsByNamePhone AS (
    SELECT 
        TenantId,
        FullName,
        PhoneNumber,
        CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY FullName, PhoneNumber 
            ORDER BY CreatedAt DESC, TenantId DESC
        ) AS RowNum
    FROM Tenant
    WHERE IsArchived = 0
)
UPDATE t
SET t.IsArchived = 1
FROM Tenant t
INNER JOIN DuplicateTenantsByNamePhone dt ON t.TenantId = dt.TenantId
WHERE dt.RowNum > 1; -- Keep only the first (most recent) one

PRINT 'Duplicate tenants by Name + Phone archived.';
GO

-- Step 3: Show summary of what was archived
PRINT 'Step 3: Summary of archived tenants...';
GO

SELECT 
    COUNT(*) AS ArchivedTenants,
    'Total archived tenants' AS Description
FROM Tenant
WHERE IsArchived = 1;

SELECT 
    COUNT(*) AS ActiveTenants,
    'Total active tenants' AS Description
FROM Tenant
WHERE IsArchived = 0;

-- Step 4: Show active tenants for verification
PRINT 'Step 4: Active tenants list...';
GO

SELECT 
    TenantId,
    FullName,
    Email,
    PhoneNumber,
    CreatedAt
FROM Tenant
WHERE IsArchived = 0
ORDER BY CreatedAt DESC;

-- Step 5: End tenancies for archived tenants
PRINT 'Step 5: Ending tenancies for archived tenants...';
GO

UPDATE t
SET t.Status = 'Ended',
    t.MoveOutDate = GETUTCDATE()
FROM Tenancy t
INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
WHERE t.Status = 'Active' AND tn.IsArchived = 1;

PRINT 'Tenancies for archived tenants ended.';
GO

-- Step 6: Show summary of ended tenancies
PRINT 'Step 6: Summary of ended tenancies...';
GO

SELECT 
    COUNT(*) AS EndedTenancies,
    'Total tenancies ended for archived tenants' AS Description
FROM Tenancy t
INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
WHERE t.Status = 'Ended' AND tn.IsArchived = 1;

PRINT 'Tenant cleanup completed!';
GO
