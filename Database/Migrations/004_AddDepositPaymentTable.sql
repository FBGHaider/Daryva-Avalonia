-- =============================================
-- Add DepositPayment Table
-- =============================================

USE [DaryvaDB]
GO

-- DepositPayment Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DepositPayment]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DepositPayment] (
        [DepositPaymentId] INT IDENTITY(1,1) NOT NULL,
        [TenancyId] INT NOT NULL,
        [PaidOn] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [AmountPaid] DECIMAL(10,2) NOT NULL,
        [Method] NVARCHAR(20) NOT NULL CHECK ([Method] IN ('BankTransfer', 'Cash', 'Card', 'Other')),
        [Reference] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_DepositPayment] PRIMARY KEY CLUSTERED ([DepositPaymentId] ASC),
        CONSTRAINT [FK_DepositPayment_Tenancy] FOREIGN KEY ([TenancyId]) 
            REFERENCES [dbo].[Tenancy] ([TenancyId]) ON DELETE NO ACTION ON UPDATE NO ACTION
    );
    PRINT 'Table DepositPayment created successfully.';
END
ELSE
BEGIN
    PRINT 'Table DepositPayment already exists.';
END
GO

-- Index on TenancyId for deposit payment lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DepositPayment_TenancyId' AND object_id = OBJECT_ID('dbo.DepositPayment'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DepositPayment_TenancyId] 
    ON [dbo].[DepositPayment] ([TenancyId] ASC)
    INCLUDE ([AmountPaid], [PaidOn]);
    PRINT 'Index IX_DepositPayment_TenancyId created.';
END
ELSE
BEGIN
    PRINT 'Index IX_DepositPayment_TenancyId already exists.';
END
GO

PRINT 'DepositPayment table and indexes created successfully!';
GO
