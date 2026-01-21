-- =============================================
-- Fix Notification Type Constraint (Simple Version)
-- This script will drop and recreate the constraint
-- =============================================

USE [DaryvaDB]
GO

PRINT 'Starting Notification Type constraint fix...';
GO

-- Drop the constraint if it exists (try both possible names)
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Notification_Type' AND parent_object_id = OBJECT_ID('dbo.Notification'))
BEGIN
    ALTER TABLE [dbo].[Notification] DROP CONSTRAINT [CK_Notification_Type];
    PRINT 'Dropped existing CK_Notification_Type constraint';
END
GO

-- Drop any auto-generated constraints on the Type column
DECLARE @ConstraintName NVARCHAR(200);
DECLARE @Sql NVARCHAR(MAX);

DECLARE constraint_cursor CURSOR FOR
SELECT name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Type', 'ColumnId');

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

-- Add the new constraint
ALTER TABLE [dbo].[Notification]
ADD CONSTRAINT [CK_Notification_Type] 
CHECK ([Type] IN ('RentDue', 'RentOverdue', 'MissingDocuments', 'General'));
GO

PRINT 'Constraint created successfully!';
PRINT 'Allowed values: RentDue, RentOverdue, MissingDocuments, General';
GO

-- Verify
SELECT 
    name AS ConstraintName,
    definition AS ConstraintDefinition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Type', 'ColumnId');
GO

PRINT 'Migration completed!';
GO
