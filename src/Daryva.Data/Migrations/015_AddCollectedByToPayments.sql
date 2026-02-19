-- =============================================
-- Migration: Add CollectedBy to RentPayment and DepositPayment
-- =============================================

USE [DaryvaDB]
GO

PRINT 'Adding CollectedBy columns to RentPayment and DepositPayment...';
GO

-- Add CollectedBy to RentPayment if missing
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RentPayment') AND name = 'CollectedBy')
BEGIN
    ALTER TABLE [dbo].[RentPayment]
    ADD [CollectedBy] NVARCHAR(100) NULL;
    PRINT 'Added CollectedBy to RentPayment.';
END
GO

-- Add CollectedBy to DepositPayment if missing
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepositPayment') AND name = 'CollectedBy')
BEGIN
    ALTER TABLE [dbo].[DepositPayment]
    ADD [CollectedBy] NVARCHAR(100) NULL;
    PRINT 'Added CollectedBy to DepositPayment.';
END
GO

-- Backfill existing payments to 'Abbas'
UPDATE [dbo].[RentPayment] SET CollectedBy = 'Abbas' WHERE CollectedBy IS NULL;
UPDATE [dbo].[DepositPayment] SET CollectedBy = 'Abbas' WHERE CollectedBy IS NULL;
GO

PRINT 'CollectedBy migration completed.';
GO
