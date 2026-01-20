-- =============================================
-- Force Fix Notification Status Constraint
-- This script will drop ALL constraints on the Status column and recreate it
-- =============================================

USE [LandLordBuddyDB]
GO

PRINT 'Force fixing Notification Status constraint...';
GO

-- Step 1: Drop ALL constraints on the Status column
DECLARE @ConstraintName NVARCHAR(200);
DECLARE @Sql NVARCHAR(MAX);

-- Create a cursor to find ALL constraints on the Status column
DECLARE constraint_cursor CURSOR FOR
SELECT name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Status', 'ColumnId');

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

-- Step 2: Add the new constraint with Cancelled status
IF NOT EXISTS (SELECT * FROM sys.check_constraints 
               WHERE parent_object_id = OBJECT_ID('dbo.Notification')
                 AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Status', 'ColumnId')
                 AND definition LIKE '%Cancelled%')
BEGIN
    ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [CK_Notification_Status] 
    CHECK ([Status] IN ('Pending', 'Sent', 'Failed', 'Cancelled'));
    PRINT 'Status constraint created with Cancelled status.';
END
ELSE
BEGIN
    PRINT 'Status constraint already includes Cancelled.';
END
GO

PRINT 'Notification Status constraint fix completed!';
GO

-- Verify the constraint
SELECT 
    name AS ConstraintName,
    definition AS ConstraintDefinition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Status', 'ColumnId');
GO
