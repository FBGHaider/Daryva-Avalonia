-- =============================================
-- Create NotificationTemplate and NotificationAttempt Tables
-- =============================================

USE [DaryvaDB]
GO

-- NotificationTemplate Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificationTemplate]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificationTemplate] (
        [TemplateId] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Channel] NVARCHAR(20) NOT NULL CHECK ([Channel] IN ('Email', 'WhatsApp')),
        [Type] NVARCHAR(50) NOT NULL CHECK ([Type] IN ('RentDue', 'RentOverdue', 'MissingDocuments', 'General')),
        [SubjectTemplate] NVARCHAR(500) NULL,
        [BodyTemplate] NVARCHAR(MAX) NOT NULL,
        [IsDefault] BIT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_NotificationTemplate] PRIMARY KEY CLUSTERED ([TemplateId] ASC)
    );
    PRINT 'Table NotificationTemplate created successfully.';
    
    -- Insert default templates
    INSERT INTO [dbo].[NotificationTemplate] ([Name], [Channel], [Type], [SubjectTemplate], [BodyTemplate], [IsDefault])
    VALUES
    ('Rent Due Reminder', 'Email', 'RentDue', 'Rent Due Reminder - {Month}', 
     'Dear {TenantName},' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'This is a reminder that your rent payment of £{AmountDue} for {Month} is due on {DueDate}.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Property: {HouseAddress}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Please ensure payment is made by the due date.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Payment Instructions: {PayInstructions}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Thank you.', 1),
    
    ('Rent Overdue', 'Email', 'RentOverdue', 'URGENT: Rent Overdue - {Month}', 
     'Dear {TenantName},' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'This is to inform you that your rent payment of £{AmountDue} for {Month} is now overdue.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Property: {HouseAddress}' + CHAR(13) + CHAR(10) + 
     'Due Date: {DueDate}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Please arrange payment immediately to avoid further action.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Payment Instructions: {PayInstructions}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Thank you.', 1),
    
    ('Missing Student Letter', 'Email', 'MissingDocuments', 'Missing Student Confirmation Letter', 
     'Dear {TenantName},' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'This is a reminder that we are still missing your Student Confirmation Letter.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Property: {HouseAddress}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Please provide this document at your earliest convenience.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Thank you.', 1),
    
    ('General Message', 'Email', 'General', 'Message from Landlord', 
     'Dear {TenantName},' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     '{Message}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Property: {HouseAddress}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 
     'Thank you.', 1);
    
    PRINT 'Default notification templates inserted.';
END
ELSE
BEGIN
    PRINT 'Table NotificationTemplate already exists.';
END
GO

-- NotificationAttempt Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificationAttempt]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificationAttempt] (
        [AttemptId] INT IDENTITY(1,1) NOT NULL,
        [NotificationId] INT NOT NULL,
        [AttemptedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Status] NVARCHAR(20) NOT NULL CHECK ([Status] IN ('Success', 'Failed')),
        [Error] NVARCHAR(MAX) NULL,
        [ProviderMessageId] NVARCHAR(200) NULL,
        CONSTRAINT [PK_NotificationAttempt] PRIMARY KEY CLUSTERED ([AttemptId] ASC),
        CONSTRAINT [FK_NotificationAttempt_Notification] FOREIGN KEY ([NotificationId]) 
            REFERENCES [dbo].[Notification] ([NotificationId]) ON DELETE CASCADE
    );
    PRINT 'Table NotificationAttempt created successfully.';
END
ELSE
BEGIN
    PRINT 'Table NotificationAttempt already exists.';
END
GO

-- Update Notification table to support new types
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Notification_Type')
BEGIN
    ALTER TABLE [dbo].[Notification] DROP CONSTRAINT [CK_Notification_Type];
    PRINT 'Existing Type constraint dropped.';
END
GO

ALTER TABLE [dbo].[Notification]
ADD CONSTRAINT [CK_Notification_Type] 
CHECK ([Type] IN ('RentDue', 'RentOverdue', 'MissingDocuments', 'General'));
GO

-- Update Notification table to support Cancelled status
-- Drop ALL existing Status constraints (by name or by finding them)
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

-- Add TemplateId to Notification table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Notification]') AND name = 'TemplateId')
BEGIN
    ALTER TABLE [dbo].[Notification]
    ADD [TemplateId] INT NULL;
    
    ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_Template] FOREIGN KEY ([TemplateId]) 
        REFERENCES [dbo].[NotificationTemplate] ([TemplateId]) ON DELETE NO ACTION;
    
    PRINT 'TemplateId column added to Notification table.';
END
ELSE
BEGIN
    PRINT 'TemplateId column already exists in Notification table.';
END
GO

PRINT 'Notification tables migration completed successfully!';
GO
