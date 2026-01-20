-- =============================================
-- Fix Notification Status Constraint to Include Cancelled
-- This script finds and updates the Status constraint regardless of its name
-- =============================================

USE [LandLordBuddyDB]
GO

PRINT 'Updating Notification Status constraint to include Cancelled...';
GO

-- Drop ALL existing Status constraints
-- First, try to drop by the standard name
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Notification_Status' AND parent_object_id = OBJECT_ID('dbo.Notification'))
BEGIN
    ALTER TABLE [dbo].[Notification] DROP CONSTRAINT [CK_Notification_Status];
    PRINT 'Dropped constraint: CK_Notification_Status';
END
GO

-- Now find and drop any other Status constraints (auto-generated names)
DECLARE @ConstraintName NVARCHAR(200);
DECLARE @Sql NVARCHAR(MAX);

DECLARE constraint_cursor CURSOR FOR
SELECT name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Status', 'ColumnId')
  AND (definition LIKE '%Status%IN%''Pending'',''Sent'',''Failed''%' 
       OR definition LIKE '%Status%IN%Pending%Sent%Failed%'
       OR definition LIKE '%[Status]%IN%(''Pending'',''Sent'',''Failed'')%');

OPEN constraint_cursor;
FETCH NEXT FROM constraint_cursor INTO @ConstraintName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = 'ALTER TABLE [dbo].[Notification] DROP CONSTRAINT [' + @ConstraintName + ']';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped constraint: ' + @ConstraintName;
    FETCH NEXT FROM constraint_cursor INTO @ConstraintName;
END

CLOSE constraint_cursor;
DEALLOCATE constraint_cursor;
GO

-- Add new constraint with Cancelled status
IF NOT EXISTS (SELECT * FROM sys.check_constraints 
               WHERE parent_object_id = OBJECT_ID('dbo.Notification')
                 AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Status', 'ColumnId')
                 AND definition LIKE '%Cancelled%')
BEGIN
    ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [CK_Notification_Status] 
    CHECK ([Status] IN ('Pending', 'Sent', 'Failed', 'Cancelled'));
    PRINT 'Status constraint updated to include Cancelled.';
END
ELSE
BEGIN
    PRINT 'Status constraint already includes Cancelled.';
END
GO

PRINT 'Notification Status constraint update completed!';
GO
