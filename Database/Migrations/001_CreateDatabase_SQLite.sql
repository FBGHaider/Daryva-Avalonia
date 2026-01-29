-- =============================================
-- Daryva Database Schema - SQLite Version
-- =============================================

-- House Table
CREATE TABLE IF NOT EXISTS House (
    HouseId INTEGER PRIMARY KEY AUTOINCREMENT,
    AddressLine1 TEXT NOT NULL,
    AddressLine2 TEXT,
    City TEXT NOT NULL,
    Postcode TEXT NOT NULL,
    TotalRooms INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Tenant Table
CREATE TABLE IF NOT EXISTS Tenant (
    TenantId INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName TEXT NOT NULL,
    PhoneNumber TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE,
    UniversityName TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    IsArchived INTEGER NOT NULL DEFAULT 0
);

-- Tenancy Table
CREATE TABLE IF NOT EXISTS Tenancy (
    TenancyId INTEGER PRIMARY KEY AUTOINCREMENT,
    HouseId INTEGER NOT NULL,
    TenantId INTEGER NOT NULL,
    MoveInDate TEXT NOT NULL,
    MoveOutDate TEXT,
    RentAmountMonthly REAL NOT NULL,
    DepositAmount REAL NOT NULL,
    PaymentDueDay INTEGER NOT NULL,
    Status TEXT NOT NULL CHECK(Status IN ('Active', 'Ended')),
    Notes TEXT,
    FOREIGN KEY (HouseId) REFERENCES House(HouseId),
    FOREIGN KEY (TenantId) REFERENCES Tenant(TenantId)
);

-- RentCharge Table
CREATE TABLE IF NOT EXISTS RentCharge (
    RentChargeId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenancyId INTEGER NOT NULL,
    PeriodYear INTEGER NOT NULL,
    PeriodMonth INTEGER NOT NULL,
    AmountDue REAL NOT NULL,
    DueDate TEXT NOT NULL,
    Status TEXT NOT NULL CHECK(Status IN ('Pending', 'Paid', 'Overdue', 'Partial')),
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (TenancyId) REFERENCES Tenancy(TenancyId),
    UNIQUE(TenancyId, PeriodYear, PeriodMonth)
);

-- RentPayment Table
CREATE TABLE IF NOT EXISTS RentPayment (
    RentPaymentId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenancyId INTEGER NOT NULL,
    RentChargeId INTEGER,
    PaidOn TEXT NOT NULL,
    AmountPaid REAL NOT NULL,
    Method TEXT,
    Reference TEXT,
    Notes TEXT,
    CollectedBy TEXT,
    FOREIGN KEY (TenancyId) REFERENCES Tenancy(TenancyId),
    FOREIGN KEY (RentChargeId) REFERENCES RentCharge(RentChargeId)
);

-- DepositPayment Table
CREATE TABLE IF NOT EXISTS DepositPayment (
    DepositPaymentId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenancyId INTEGER NOT NULL,
    PaidOn TEXT NOT NULL,
    AmountPaid REAL NOT NULL,
    Method TEXT,
    Reference TEXT,
    Notes TEXT,
    CollectedBy TEXT,
    FOREIGN KEY (TenancyId) REFERENCES Tenancy(TenancyId)
);

-- Document Table
CREATE TABLE IF NOT EXISTS Document (
    DocumentId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenantId INTEGER,
    TenancyId INTEGER,
    HouseId INTEGER NOT NULL,
    Type TEXT NOT NULL,
    FileName TEXT NOT NULL,
    StoragePath TEXT NOT NULL,
    FileMimeType TEXT,
    Version INTEGER NOT NULL DEFAULT 1,
    IsActive INTEGER NOT NULL DEFAULT 1,
    UploadedAt TEXT NOT NULL DEFAULT (datetime('now')),
    DisplayName TEXT,
    Source TEXT,
    FOREIGN KEY (TenantId) REFERENCES Tenant(TenantId),
    FOREIGN KEY (TenancyId) REFERENCES Tenancy(TenancyId),
    FOREIGN KEY (HouseId) REFERENCES House(HouseId),
    CHECK((TenantId IS NOT NULL) OR (TenancyId IS NOT NULL) OR (HouseId IS NOT NULL))
);

-- HouseExpense Table
CREATE TABLE IF NOT EXISTS HouseExpense (
    HouseExpenseId INTEGER PRIMARY KEY AUTOINCREMENT,
    HouseId INTEGER NOT NULL,
    DateIncurred TEXT NOT NULL,
    Category TEXT NOT NULL,
    Amount REAL NOT NULL,
    Vendor TEXT,
    Notes TEXT,
    ReceiptDocumentId INTEGER,
    FOREIGN KEY (HouseId) REFERENCES House(HouseId),
    FOREIGN KEY (ReceiptDocumentId) REFERENCES Document(DocumentId)
);

-- Notification Table
CREATE TABLE IF NOT EXISTS Notification (
    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenantId INTEGER NOT NULL,
    TenancyId INTEGER,
    Channel TEXT NOT NULL CHECK(Channel IN ('Email', 'WhatsApp', 'SMS')),
    Type TEXT NOT NULL,
    ToAddress TEXT NOT NULL,
    Subject TEXT,
    Body TEXT NOT NULL,
    ScheduledFor TEXT NOT NULL,
    SentAt TEXT,
    Status TEXT NOT NULL CHECK(Status IN ('Pending', 'Sent', 'Failed')),
    ProviderMessageId TEXT,
    Error TEXT,
    TemplateId INTEGER,
    FOREIGN KEY (TenantId) REFERENCES Tenant(TenantId),
    FOREIGN KEY (TenancyId) REFERENCES Tenancy(TenancyId)
);

-- NotificationTemplate Table
CREATE TABLE IF NOT EXISTS NotificationTemplate (
    TemplateId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Channel TEXT NOT NULL,
    Type TEXT NOT NULL,
    SubjectTemplate TEXT,
    BodyTemplate TEXT NOT NULL,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- NotificationAttempt Table
CREATE TABLE IF NOT EXISTS NotificationAttempt (
    AttemptId INTEGER PRIMARY KEY AUTOINCREMENT,
    NotificationId INTEGER NOT NULL,
    AttemptedAt TEXT NOT NULL,
    Status TEXT NOT NULL,
    Error TEXT,
    ProviderMessageId TEXT,
    FOREIGN KEY (NotificationId) REFERENCES Notification(NotificationId)
);

-- AppSettings Table
CREATE TABLE IF NOT EXISTS AppSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT,
    SettingType TEXT NOT NULL DEFAULT 'String',
    Category TEXT NOT NULL DEFAULT 'General',
    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- =============================================
-- Indexes for Performance
-- =============================================

CREATE INDEX IF NOT EXISTS IX_Tenancy_HouseId_Status ON Tenancy(HouseId, Status);
CREATE INDEX IF NOT EXISTS IX_Tenancy_TenantId_Status ON Tenancy(TenantId, Status);
CREATE INDEX IF NOT EXISTS IX_RentCharge_TenancyId ON RentCharge(TenancyId);
CREATE INDEX IF NOT EXISTS IX_RentPayment_RentChargeId ON RentPayment(RentChargeId);
CREATE INDEX IF NOT EXISTS IX_RentPayment_TenancyId ON RentPayment(TenancyId);
CREATE INDEX IF NOT EXISTS IX_DepositPayment_TenancyId ON DepositPayment(TenancyId);
CREATE INDEX IF NOT EXISTS IX_Document_TenantId ON Document(TenantId);
CREATE INDEX IF NOT EXISTS IX_Document_TenancyId ON Document(TenancyId);
CREATE INDEX IF NOT EXISTS IX_Document_HouseId ON Document(HouseId);
CREATE INDEX IF NOT EXISTS IX_Document_Type_IsActive ON Document(Type, IsActive);
CREATE INDEX IF NOT EXISTS IX_HouseExpense_HouseId ON HouseExpense(HouseId);
CREATE INDEX IF NOT EXISTS IX_HouseExpense_DateIncurred ON HouseExpense(DateIncurred);
CREATE INDEX IF NOT EXISTS IX_Notification_Status_ScheduledFor ON Notification(Status, ScheduledFor);
CREATE INDEX IF NOT EXISTS IX_Notification_TenantId ON Notification(TenantId);
CREATE INDEX IF NOT EXISTS IX_Notification_TenancyId ON Notification(TenancyId);
CREATE INDEX IF NOT EXISTS IX_NotificationAttempt_NotificationId ON NotificationAttempt(NotificationId);
