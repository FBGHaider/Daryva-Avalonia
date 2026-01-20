-- =============================================
-- LandLord Buddy Database Schema
-- SQL Server Migration Script
-- =============================================

USE [LandLordBuddyDB]
GO

-- =============================================
-- 1. CORE ENTITIES
-- =============================================

-- House Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[House]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[House] (
        [HouseId] INT IDENTITY(1,1) NOT NULL,
        [AddressLine1] NVARCHAR(200) NOT NULL,
        [AddressLine2] NVARCHAR(200) NULL,
        [City] NVARCHAR(100) NOT NULL,
        [Postcode] NVARCHAR(20) NOT NULL,
        [TotalRooms] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_House] PRIMARY KEY CLUSTERED ([HouseId] ASC)
    );
    PRINT 'Table House created successfully.';
END
ELSE
BEGIN
    PRINT 'Table House already exists.';
END
GO

-- Tenant Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Tenant]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Tenant] (
        [TenantId] INT IDENTITY(1,1) NOT NULL,
        [FullName] NVARCHAR(200) NOT NULL,
        [PhoneNumber] NVARCHAR(20) NOT NULL,
        [Email] NVARCHAR(255) NOT NULL,
        [UniversityName] NVARCHAR(200) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsArchived] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Tenant] PRIMARY KEY CLUSTERED ([TenantId] ASC),
        CONSTRAINT [UQ_Tenant_Email] UNIQUE NONCLUSTERED ([Email] ASC)
    );
    PRINT 'Table Tenant created successfully.';
END
ELSE
BEGIN
    PRINT 'Table Tenant already exists.';
END
GO

-- Tenancy Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Tenancy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Tenancy] (
        [TenancyId] INT IDENTITY(1,1) NOT NULL,
        [HouseId] INT NOT NULL,
        [TenantId] INT NOT NULL,
        [MoveInDate] DATE NOT NULL,
        [MoveOutDate] DATE NULL,
        [RentAmountMonthly] DECIMAL(10,2) NOT NULL,
        [DepositAmount] DECIMAL(10,2) NOT NULL,
        [PaymentDueDay] TINYINT NOT NULL CHECK ([PaymentDueDay] BETWEEN 1 AND 28),
        [Status] NVARCHAR(20) NOT NULL CHECK ([Status] IN ('Active', 'Ended')),
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_Tenancy] PRIMARY KEY CLUSTERED ([TenancyId] ASC),
        CONSTRAINT [FK_Tenancy_House] FOREIGN KEY ([HouseId]) 
            REFERENCES [dbo].[House] ([HouseId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [FK_Tenancy_Tenant] FOREIGN KEY ([TenantId]) 
            REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION ON UPDATE NO ACTION
    );
    PRINT 'Table Tenancy created successfully.';
END
ELSE
BEGIN
    PRINT 'Table Tenancy already exists.';
END
GO

-- =============================================
-- 2. RENT TRACKING
-- =============================================

-- RentCharge Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RentCharge]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RentCharge] (
        [RentChargeId] INT IDENTITY(1,1) NOT NULL,
        [TenancyId] INT NOT NULL,
        [PeriodYear] INT NOT NULL,
        [PeriodMonth] INT NOT NULL CHECK ([PeriodMonth] BETWEEN 1 AND 12),
        [AmountDue] DECIMAL(10,2) NOT NULL,
        [DueDate] DATE NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CHECK ([Status] IN ('Unpaid', 'PartPaid', 'Paid', 'Late')),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_RentCharge] PRIMARY KEY CLUSTERED ([RentChargeId] ASC),
        CONSTRAINT [FK_RentCharge_Tenancy] FOREIGN KEY ([TenancyId]) 
            REFERENCES [dbo].[Tenancy] ([TenancyId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [UQ_RentCharge_Tenancy_Period] UNIQUE NONCLUSTERED ([TenancyId] ASC, [PeriodYear] ASC, [PeriodMonth] ASC)
    );
    PRINT 'Table RentCharge created successfully.';
END
ELSE
BEGIN
    PRINT 'Table RentCharge already exists.';
END
GO

-- RentPayment Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RentPayment]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RentPayment] (
        [RentPaymentId] INT IDENTITY(1,1) NOT NULL,
        [TenancyId] INT NOT NULL,
        [RentChargeId] INT NULL,
        [PaidOn] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [AmountPaid] DECIMAL(10,2) NOT NULL,
        [Method] NVARCHAR(20) NOT NULL CHECK ([Method] IN ('BankTransfer', 'Cash', 'Card', 'Other')),
        [Reference] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_RentPayment] PRIMARY KEY CLUSTERED ([RentPaymentId] ASC),
        CONSTRAINT [FK_RentPayment_Tenancy] FOREIGN KEY ([TenancyId]) 
            REFERENCES [dbo].[Tenancy] ([TenancyId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [FK_RentPayment_RentCharge] FOREIGN KEY ([RentChargeId]) 
            REFERENCES [dbo].[RentCharge] ([RentChargeId]) ON DELETE NO ACTION ON UPDATE NO ACTION
    );
    PRINT 'Table RentPayment created successfully.';
END
ELSE
BEGIN
    PRINT 'Table RentPayment already exists.';
END
GO

-- =============================================
-- 3. DOCUMENT STORAGE
-- =============================================

-- Document Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Document]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Document] (
        [DocumentId] INT IDENTITY(1,1) NOT NULL,
        [TenantId] INT NULL,
        [TenancyId] INT NULL,
        [HouseId] INT NULL,
        [Type] NVARCHAR(50) NOT NULL CHECK ([Type] IN ('StudentConfirmationLetter', 'TenancyContract', 'ID', 'Other')),
        [FileName] NVARCHAR(255) NOT NULL,
        [FileMimeType] NVARCHAR(100) NULL,
        [StoragePath] NVARCHAR(500) NULL,
        [FileBytes] VARBINARY(MAX) NULL,
        [UploadedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ValidFrom] DATE NULL,
        [ValidTo] DATE NULL,
        [Version] INT NOT NULL DEFAULT 1,
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_Document] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
        CONSTRAINT [FK_Document_Tenant] FOREIGN KEY ([TenantId]) 
            REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [FK_Document_Tenancy] FOREIGN KEY ([TenancyId]) 
            REFERENCES [dbo].[Tenancy] ([TenancyId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [FK_Document_House] FOREIGN KEY ([HouseId]) 
            REFERENCES [dbo].[House] ([HouseId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [CHK_Document_AtLeastOneReference] CHECK (
            [TenantId] IS NOT NULL OR [TenancyId] IS NOT NULL OR [HouseId] IS NOT NULL
        )
    );
    PRINT 'Table Document created successfully.';
END
ELSE
BEGIN
    PRINT 'Table Document already exists.';
END
GO

-- =============================================
-- 4. HOUSE EXPENSES
-- =============================================

-- HouseExpense Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HouseExpense]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HouseExpense] (
        [HouseExpenseId] INT IDENTITY(1,1) NOT NULL,
        [HouseId] INT NOT NULL,
        [DateIncurred] DATE NOT NULL,
        [Category] NVARCHAR(50) NOT NULL CHECK ([Category] IN ('Repairs', 'Bills', 'CouncilTax', 'Internet', 'Furniture', 'Cleaning', 'Maintenance', 'Other')),
        [Amount] DECIMAL(10,2) NOT NULL,
        [Vendor] NVARCHAR(200) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [ReceiptDocumentId] INT NULL,
        CONSTRAINT [PK_HouseExpense] PRIMARY KEY CLUSTERED ([HouseExpenseId] ASC),
        CONSTRAINT [FK_HouseExpense_House] FOREIGN KEY ([HouseId]) 
            REFERENCES [dbo].[House] ([HouseId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [FK_HouseExpense_Document] FOREIGN KEY ([ReceiptDocumentId]) 
            REFERENCES [dbo].[Document] ([DocumentId]) ON DELETE NO ACTION ON UPDATE NO ACTION
    );
    PRINT 'Table HouseExpense created successfully.';
END
ELSE
BEGIN
    PRINT 'Table HouseExpense already exists.';
END
GO

-- =============================================
-- 5. NOTIFICATIONS
-- =============================================

-- Notification Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Notification]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Notification] (
        [NotificationId] INT IDENTITY(1,1) NOT NULL,
        [TenantId] INT NOT NULL,
        [TenancyId] INT NULL,
        [Channel] NVARCHAR(20) NOT NULL CHECK ([Channel] IN ('Email', 'WhatsApp')),
        [Type] NVARCHAR(50) NOT NULL CHECK ([Type] IN ('RentDue', 'RentLate', 'DocumentExpiring', 'General')),
        [ToAddress] NVARCHAR(255) NOT NULL,
        [Subject] NVARCHAR(500) NULL,
        [Body] NVARCHAR(MAX) NOT NULL,
        [ScheduledFor] DATETIME2 NOT NULL,
        [SentAt] DATETIME2 NULL,
        [Status] NVARCHAR(20) NOT NULL CHECK ([Status] IN ('Pending', 'Sent', 'Failed')),
        [ProviderMessageId] NVARCHAR(200) NULL,
        [Error] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([NotificationId] ASC),
        CONSTRAINT [FK_Notification_Tenant] FOREIGN KEY ([TenantId]) 
            REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT [FK_Notification_Tenancy] FOREIGN KEY ([TenancyId]) 
            REFERENCES [dbo].[Tenancy] ([TenancyId]) ON DELETE NO ACTION ON UPDATE NO ACTION
    );
    PRINT 'Table Notification created successfully.';
END
ELSE
BEGIN
    PRINT 'Table Notification already exists.';
END
GO

-- =============================================
-- 6. CONTRACT TERMS (OPTIONAL)
-- =============================================

-- TenancyRule Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TenancyRule]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TenancyRule] (
        [ClauseId] INT IDENTITY(1,1) NOT NULL,
        [TenancyId] INT NOT NULL,
        [Type] NVARCHAR(50) NOT NULL CHECK ([Type] IN ('NoticePeriodDays', 'FixedTermEnd', 'EarlyTerminationFee', 'ReplacementTenantRequired', 'Other')),
        [Value] NVARCHAR(500) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_TenancyRule] PRIMARY KEY CLUSTERED ([ClauseId] ASC),
        CONSTRAINT [FK_TenancyRule_Tenancy] FOREIGN KEY ([TenancyId]) 
            REFERENCES [dbo].[Tenancy] ([TenancyId]) ON DELETE NO ACTION ON UPDATE NO ACTION
    );
    PRINT 'Table TenancyRule created successfully.';
END
ELSE
BEGIN
    PRINT 'Table TenancyRule already exists.';
END
GO

PRINT 'All tables created successfully!';
GO
