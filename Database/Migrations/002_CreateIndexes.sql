-- =============================================
-- LandLord Buddy Database - Indexes and Performance
-- =============================================

USE [LandLordBuddyDB]
GO

-- =============================================
-- Tenancy Indexes
-- =============================================

-- Index on (HouseId, Status) for querying active tenancies per house
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tenancy_HouseId_Status' AND object_id = OBJECT_ID('dbo.Tenancy'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Tenancy_HouseId_Status] 
    ON [dbo].[Tenancy] ([HouseId] ASC, [Status] ASC)
    INCLUDE ([TenantId], [MoveInDate], [MoveOutDate], [RentAmountMonthly]);
    PRINT 'Index IX_Tenancy_HouseId_Status created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Tenancy_HouseId_Status already exists.';
END
GO

-- Index on (TenantId, Status) for querying tenant's active tenancies
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tenancy_TenantId_Status' AND object_id = OBJECT_ID('dbo.Tenancy'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Tenancy_TenantId_Status] 
    ON [dbo].[Tenancy] ([TenantId] ASC, [Status] ASC)
    INCLUDE ([HouseId], [MoveInDate], [MoveOutDate], [RentAmountMonthly]);
    PRINT 'Index IX_Tenancy_TenantId_Status created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Tenancy_TenantId_Status already exists.';
END
GO

-- =============================================
-- Document Indexes
-- =============================================

-- Index on (TenancyId, Type, IsActive) for contract lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Document_TenancyId_Type_IsActive' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Document_TenancyId_Type_IsActive] 
    ON [dbo].[Document] ([TenancyId] ASC, [Type] ASC, [IsActive] ASC)
    WHERE [TenancyId] IS NOT NULL;
    PRINT 'Index IX_Document_TenancyId_Type_IsActive created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Document_TenancyId_Type_IsActive already exists.';
END
GO

-- Index on (TenantId, Type, IsActive) for tenant document lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Document_TenantId_Type_IsActive' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Document_TenantId_Type_IsActive] 
    ON [dbo].[Document] ([TenantId] ASC, [Type] ASC, [IsActive] ASC)
    WHERE [TenantId] IS NOT NULL;
    PRINT 'Index IX_Document_TenantId_Type_IsActive created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Document_TenantId_Type_IsActive already exists.';
END
GO

-- Index on ValidTo for expiring documents
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Document_ValidTo_IsActive' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Document_ValidTo_IsActive] 
    ON [dbo].[Document] ([ValidTo] ASC, [IsActive] ASC)
    WHERE [ValidTo] IS NOT NULL AND [IsActive] = 1;
    PRINT 'Index IX_Document_ValidTo_IsActive created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Document_ValidTo_IsActive already exists.';
END
GO

-- =============================================
-- Notification Indexes
-- =============================================

-- Index on (Status, ScheduledFor) for pending notifications
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notification_Status_ScheduledFor' AND object_id = OBJECT_ID('dbo.Notification'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Notification_Status_ScheduledFor] 
    ON [dbo].[Notification] ([Status] ASC, [ScheduledFor] ASC)
    INCLUDE ([TenantId], [Channel], [Type]);
    PRINT 'Index IX_Notification_Status_ScheduledFor created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Notification_Status_ScheduledFor already exists.';
END
GO

-- Index on TenantId for tenant notification history
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notification_TenantId' AND object_id = OBJECT_ID('dbo.Notification'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Notification_TenantId] 
    ON [dbo].[Notification] ([TenantId] ASC)
    INCLUDE ([SentAt], [Status], [Type]);
    PRINT 'Index IX_Notification_TenantId created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Notification_TenantId already exists.';
END
GO

-- =============================================
-- RentCharge Indexes
-- =============================================

-- Index on TenancyId and DueDate for rent queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RentCharge_TenancyId_DueDate' AND object_id = OBJECT_ID('dbo.RentCharge'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RentCharge_TenancyId_DueDate] 
    ON [dbo].[RentCharge] ([TenancyId] ASC, [DueDate] ASC)
    INCLUDE ([Status], [AmountDue]);
    PRINT 'Index IX_RentCharge_TenancyId_DueDate created.';
END
ELSE
BEGIN
    PRINT 'Index IX_RentCharge_TenancyId_DueDate already exists.';
END
GO

-- Index on Status for unpaid/late charges
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RentCharge_Status_DueDate' AND object_id = OBJECT_ID('dbo.RentCharge'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RentCharge_Status_DueDate] 
    ON [dbo].[RentCharge] ([Status] ASC, [DueDate] ASC)
    WHERE [Status] IN ('Unpaid', 'PartPaid', 'Late');
    PRINT 'Index IX_RentCharge_Status_DueDate created.';
END
ELSE
BEGIN
    PRINT 'Index IX_RentCharge_Status_DueDate already exists.';
END
GO

-- =============================================
-- RentPayment Indexes
-- =============================================

-- Index on RentChargeId for payment lookups
-- Note: Filtered indexes cannot use INCLUDE, so we index on RentChargeId with AmountPaid and PaidOn as key columns
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RentPayment_RentChargeId' AND object_id = OBJECT_ID('dbo.RentPayment'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RentPayment_RentChargeId] 
    ON [dbo].[RentPayment] ([RentChargeId] ASC, [AmountPaid] ASC, [PaidOn] ASC)
    WHERE [RentChargeId] IS NOT NULL;
    PRINT 'Index IX_RentPayment_RentChargeId created.';
END
ELSE
BEGIN
    PRINT 'Index IX_RentPayment_RentChargeId already exists.';
END
GO

-- Index on TenancyId and PaidOn for payment history
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RentPayment_TenancyId_PaidOn' AND object_id = OBJECT_ID('dbo.RentPayment'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RentPayment_TenancyId_PaidOn] 
    ON [dbo].[RentPayment] ([TenancyId] ASC, [PaidOn] DESC)
    INCLUDE ([AmountPaid], [Method]);
    PRINT 'Index IX_RentPayment_TenancyId_PaidOn created.';
END
ELSE
BEGIN
    PRINT 'Index IX_RentPayment_TenancyId_PaidOn already exists.';
END
GO

-- =============================================
-- HouseExpense Indexes
-- =============================================

-- Index on HouseId and DateIncurred for expense reports
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HouseExpense_HouseId_DateIncurred' AND object_id = OBJECT_ID('dbo.HouseExpense'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HouseExpense_HouseId_DateIncurred] 
    ON [dbo].[HouseExpense] ([HouseId] ASC, [DateIncurred] DESC)
    INCLUDE ([Category], [Amount]);
    PRINT 'Index IX_HouseExpense_HouseId_DateIncurred created.';
END
ELSE
BEGIN
    PRINT 'Index IX_HouseExpense_HouseId_DateIncurred already exists.';
END
GO

PRINT 'All indexes created successfully!';
GO
