-- =============================================
-- Update HouseExpense Category Constraint
-- Add Cleaning and Maintenance categories
-- =============================================

USE [DaryvaDB]
GO

PRINT 'Updating HouseExpense Category constraint...';
GO

-- Drop existing constraint
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_HouseExpense_Category')
BEGIN
    ALTER TABLE [dbo].[HouseExpense] DROP CONSTRAINT [CK_HouseExpense_Category];
    PRINT 'Existing Category constraint dropped.';
END
GO

-- Add new constraint with additional categories
ALTER TABLE [dbo].[HouseExpense]
ADD CONSTRAINT [CK_HouseExpense_Category] 
CHECK ([Category] IN ('Repairs', 'Bills', 'CouncilTax', 'Internet', 'Furniture', 'Cleaning', 'Maintenance', 'Other'));
GO

PRINT 'HouseExpense Category constraint updated successfully!';
GO
