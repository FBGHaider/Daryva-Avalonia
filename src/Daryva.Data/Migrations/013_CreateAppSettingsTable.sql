-- Create AppSettings table for storing application settings
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AppSettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AppSettings] (
        [SettingKey] NVARCHAR(100) NOT NULL PRIMARY KEY,
        [SettingValue] NVARCHAR(MAX) NULL,
        [SettingType] NVARCHAR(50) NOT NULL DEFAULT 'String', -- String, Int, Bool, Decimal, DateTime
        [Category] NVARCHAR(50) NOT NULL DEFAULT 'General', -- General, Rent, Documents, Notifications, Email, Backup
        [Description] NVARCHAR(500) NULL,
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [CK_AppSettings_SettingType] CHECK ([SettingType] IN ('String', 'Int', 'Bool', 'Decimal', 'DateTime'))
    );

    -- Create index on Category for faster lookups
    CREATE INDEX [IX_AppSettings_Category] ON [dbo].[AppSettings]([Category]);

    PRINT 'AppSettings table created successfully.';
END
ELSE
BEGIN
    PRINT 'AppSettings table already exists.';
END
GO

-- Insert default settings
IF NOT EXISTS (SELECT * FROM [dbo].[AppSettings] WHERE [SettingKey] = 'Currency')
BEGIN
    INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [SettingType], [Category], [Description])
    VALUES 
        -- General Settings
        ('Currency', 'GBP', 'String', 'General', 'Default currency symbol'),
        ('DateFormat', 'dd/MM/yyyy', 'String', 'General', 'Date format for display'),
        ('TimeZone', 'GMT Standard Time', 'String', 'General', 'Application time zone'),
        ('AppStartPage', 'Dashboard', 'String', 'General', 'Default page to show on app start'),
        ('ConfirmDestructiveActions', 'true', 'Bool', 'General', 'Show confirmation dialogs for destructive actions'),
        ('AutoRefreshDashboard', 'true', 'Bool', 'General', 'Automatically refresh dashboard data'),
        ('Theme', 'Dark', 'String', 'General', 'Application theme (Dark or Light)'),
        
        -- Rent & Finance Settings
        ('DefaultRentDueDay', '1', 'Int', 'Rent', 'Default day of month when rent is due'),
        ('RentGracePeriodDays', '3', 'Int', 'Rent', 'Number of days grace period before rent is considered overdue'),
        ('AllowPartialPayments', 'true', 'Bool', 'Rent', 'Allow tenants to make partial rent payments'),
        ('AllowOverpayment', 'true', 'Bool', 'Rent', 'Allow overpayments to create credit balance'),
        
        -- Document Settings
        ('MaxFileSizeMB', '10', 'Int', 'Documents', 'Maximum file size for document uploads in MB'),
        ('AllowedFormats', 'PDF,JPG,JPEG,PNG,DOCX', 'String', 'Documents', 'Comma-separated list of allowed file formats'),
        ('AutoVersionDocuments', 'true', 'Bool', 'Documents', 'Automatically create new version when uploading same document type'),
        ('StudentLetterValidityMonths', '12', 'Int', 'Documents', 'Validity period for student confirmation letters in months'),
        ('NotifyBeforeExpiryDays', '30', 'Int', 'Documents', 'Number of days before expiry to send notification'),
        ('DocumentStoragePath', '', 'String', 'Documents', 'Local folder path for document storage'),
        ('OpenDocumentsInApp', 'false', 'Bool', 'Documents', 'Open documents in app viewer instead of system default'),
        
        -- Notification Settings
        ('RentDueReminderEnabled', 'true', 'Bool', 'Notifications', 'Enable rent due reminders'),
        ('RentDueReminderDaysBefore', '3', 'Int', 'Notifications', 'Send rent due reminder X days before due date'),
        ('RentOverdueReminderEnabled', 'true', 'Bool', 'Notifications', 'Enable rent overdue reminders'),
        ('RentOverdueReminderDaysAfter', '1', 'Int', 'Notifications', 'Send rent overdue reminder X days after due date'),
        ('DocumentExpiryReminderEnabled', 'true', 'Bool', 'Notifications', 'Enable document expiry reminders'),
        ('DocumentExpiryReminderDaysBefore', '30', 'Int', 'Notifications', 'Send document expiry reminder X days before expiry'),
        ('QuietHoursStart', '22:00', 'String', 'Notifications', 'Start time for quiet hours (no notifications)'),
        ('QuietHoursEnd', '08:00', 'String', 'Notifications', 'End time for quiet hours (no notifications)'),
        ('SendOnWeekends', 'true', 'Bool', 'Notifications', 'Send notifications on weekends'),
        ('DefaultNotificationChannel', 'Email', 'String', 'Notifications', 'Default channel for notifications'),
        
        -- Email Settings (stored in App.config.local, but we track status here)
        ('EmailStatus', 'NotConfigured', 'String', 'Email', 'Email configuration status'),
        ('EmailLastTestTime', NULL, 'DateTime', 'Email', 'Last time email was successfully tested'),
        
        -- Backup Settings
        ('BackupLocation', '', 'String', 'Backup', 'Default location for database backups'),
        ('AutoBackupEnabled', 'false', 'Bool', 'Backup', 'Enable automatic backups'),
        ('AutoBackupFrequency', 'Daily', 'String', 'Backup', 'Frequency of automatic backups (Daily/Weekly)'),
        ('BackupRetentionCount', '30', 'Int', 'Backup', 'Number of backups to retain'),
        
        -- Advanced Settings
        ('DebugLoggingEnabled', 'false', 'Bool', 'Advanced', 'Enable debug logging'),
        ('LogFileLocation', '', 'String', 'Advanced', 'Location for log files');

    PRINT 'Default settings inserted successfully.';
END
ELSE
BEGIN
    PRINT 'Default settings already exist.';
END
GO
