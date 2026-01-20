-- =============================================
-- Update Document Types
-- Migration Script 005
-- =============================================

USE [DaryvaDB]
GO

-- Drop existing check constraint on Type
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK__Document__Type__xxxxx' OR name LIKE 'CK__Document__Type%')
BEGIN
    DECLARE @constraintName NVARCHAR(255);
    SELECT TOP 1 @constraintName = name 
    FROM sys.check_constraints 
    WHERE parent_object_id = OBJECT_ID('dbo.Document') 
    AND definition LIKE '%Type%';
    
    IF @constraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [dbo].[Document] DROP CONSTRAINT [' + @constraintName + ']');
        PRINT 'Dropped old Type check constraint.';
    END
END
GO

-- Add new check constraint with all document types
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Document') AND definition LIKE '%StudentConfirmationLetter%')
BEGIN
    ALTER TABLE [dbo].[Document]
    ADD CONSTRAINT [CHK_Document_Type] CHECK ([Type] IN (
        'StudentConfirmationLetter',
        'PhotoId',
        'RightToRent',
        'TenancyAgreementSigned',
        'GuarantorAgreement',
        'InventoryCheckIn',
        'DepositProtectionCertificate',
        'NoticeToLeave',
        'Other'
    ));
    PRINT 'Added new Type check constraint with all document types.';
END
ELSE
BEGIN
    PRINT 'Type check constraint already updated.';
END
GO

-- Add DisplayName column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Document') AND name = 'DisplayName')
BEGIN
    ALTER TABLE [dbo].[Document]
    ADD [DisplayName] NVARCHAR(255) NULL;
    PRINT 'Added DisplayName column.';
END
ELSE
BEGIN
    PRINT 'DisplayName column already exists.';
END
GO

-- Add Source column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Document') AND name = 'Source')
BEGIN
    ALTER TABLE [dbo].[Document]
    ADD [Source] NVARCHAR(20) NULL CHECK ([Source] IS NULL OR [Source] IN ('Uploaded', 'Generated'));
    PRINT 'Added Source column.';
END
ELSE
BEGIN
    PRINT 'Source column already exists.';
END
GO

PRINT 'Document table updated successfully!';
GO
