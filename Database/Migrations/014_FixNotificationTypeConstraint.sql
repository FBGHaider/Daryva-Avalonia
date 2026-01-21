-- =============================================
-- Fix Notification Type Constraint
-- This script will drop ALL constraints on the Type column and recreate it
-- with the correct allowed values
-- =============================================

USE [DaryvaDB]
GO

PRINT '========================================';
PRINT 'Starting Notification Type constraint fix...';
PRINT '========================================';
GO

-- Step 1: Drop ALL constraints on the Type column
PRINT 'Step 1: Dropping existing Type constraints...';
GO

DECLARE @ConstraintName NVARCHAR(200);
DECLARE @Sql NVARCHAR(MAX);
DECLARE @Count INT = 0;

-- Create a cursor to find ALL constraints on the Type column
DECLARE constraint_cursor CURSOR FOR
SELECT name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Type', 'ColumnId');

OPEN constraint_cursor;
FETCH NEXT FROM constraint_cursor INTO @ConstraintName;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @Sql = 'ALTER TABLE [dbo].[Notification] DROP CONSTRAINT [' + @ConstraintName + ']';
        EXEC sp_executesql @Sql;
        PRINT '  ✓ Dropped constraint: ' + @ConstraintName;
        SET @Count = @Count + 1;
    END TRY
    BEGIN CATCH
        PRINT '  ✗ Error dropping constraint ' + @ConstraintName + ': ' + ERROR_MESSAGE();
    END CATCH
    
    FETCH NEXT FROM constraint_cursor INTO @ConstraintName;
END

CLOSE constraint_cursor;
DEALLOCATE constraint_cursor;

IF @Count > 0
    PRINT '  Total constraints dropped: ' + CAST(@Count AS NVARCHAR(10));
ELSE
    PRINT '  No constraints found to drop.';
GO

-- Step 2: Add the new constraint with correct types
PRINT '';
PRINT 'Step 2: Creating new Type constraint...';
GO

BEGIN TRY
    ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [CK_Notification_Type] 
    CHECK ([Type] IN ('RentDue', 'RentOverdue', 'MissingDocuments', 'General'));
    PRINT '  ✓ Type constraint created successfully!';
    PRINT '  Allowed values: RentDue, RentOverdue, MissingDocuments, General';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 2714  -- Constraint already exists
    BEGIN
        PRINT '  ℹ Constraint already exists. Dropping and recreating...';
        ALTER TABLE [dbo].[Notification] DROP CONSTRAINT [CK_Notification_Type];
        ALTER TABLE [dbo].[Notification]
        ADD CONSTRAINT [CK_Notification_Type] 
        CHECK ([Type] IN ('RentDue', 'RentOverdue', 'MissingDocuments', 'General'));
        PRINT '  ✓ Constraint recreated successfully!';
    END
    ELSE
    BEGIN
        PRINT '  ✗ Error creating constraint: ' + ERROR_MESSAGE();
        THROW;
    END
END CATCH
GO

PRINT '';
PRINT '========================================';
PRINT 'Notification Type constraint fix completed!';
PRINT '========================================';
GO

-- Verify the constraint
PRINT '';
PRINT 'Verifying constraint...';
SELECT 
    name AS ConstraintName,
    definition AS ConstraintDefinition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Notification')
  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Notification'), 'Type', 'ColumnId');
GO

PRINT '';
PRINT 'Migration completed successfully!';
PRINT 'You can now send emails using the Missing Student Letter template.';
GO
