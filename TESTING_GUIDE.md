# Daryva Testing Guide

Complete guide for testing Daryva on Windows and macOS after cross-platform migration.

## Prerequisites

### Windows
- .NET 8 SDK installed
- Visual Studio 2022 or VS Code (optional, for debugging)

### macOS
- .NET 8 SDK installed
- Terminal access

## Quick Start Testing

### Windows Testing

#### 1. Build and Run
```powershell
cd Daryva-Avalonia
dotnet restore
dotnet build
dotnet run
```

**Expected Result**: Application window opens.

#### 2. Check Application Data
After first run, verify directories are created:
- `%AppData%\Daryva\` - Application data
- `%AppData%\Daryva\Database\` - Database location
- `%AppData%\Daryva\Logs\` - Log files
- `%UserProfile%\Documents\Daryva Exports\` - Export files

#### 3. Database Setup (SQLite)

The app now uses SQLite. You need to create the database schema.

**Option A: Use SQLite Browser (Recommended)**
1. Download [DB Browser for SQLite](https://sqlitebrowser.org/)
2. Create new database: `%AppData%\Daryva\Database\DaryvaDB.db`
3. Run the SQLite migration script (see below)

**Option B: Use Command Line**
```powershell
# Navigate to database directory
cd "$env:APPDATA\Daryva\Database"

# Create database file (if not exists)
# The app will create it automatically, but you need to run migrations
```

**Option C: Let the app create it, then run migrations**
- The app will create an empty database file on first run
- You'll need to convert SQL Server migrations to SQLite format (see below)

### macOS Testing

#### 1. Build and Run
```bash
cd Daryva-Avalonia
dotnet restore
dotnet build
dotnet run
```

**Expected Result**: Application window opens.

#### 2. Check Application Data
After first run, verify directories:
```bash
# Check directories
ls -la ~/Library/Application\ Support/Daryva/
ls -la ~/Documents/Daryva\ Exports/

# Check database location
ls -la ~/Library/Application\ Support/Daryva/Database/
```

#### 3. Database Setup (SQLite)

Same as Windows - create SQLite database and run migrations.

## Database Schema Setup

### Converting SQL Server Migrations to SQLite

The existing migrations in `Database/Migrations/` are SQL Server format. You need to convert them to SQLite.

**Key differences:**
- `IDENTITY(1,1)` → `INTEGER PRIMARY KEY AUTOINCREMENT`
- `NVARCHAR(n)` → `TEXT`
- `DATETIME2` → `TEXT` or `DATETIME`
- `BIT` → `INTEGER` (0 or 1)
- `GETUTCDATE()` → `datetime('now')`
- Remove `[dbo]` schema references
- Remove `GO` statements
- Remove square brackets `[]` (optional, SQLite supports them)

### Quick SQLite Schema Creation

Create a file `Database/Migrations/001_CreateDatabase_SQLite.sql`:

```sql
-- SQLite version of Daryva Database Schema

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

-- Create Indexes
CREATE INDEX IF NOT EXISTS IX_Tenancy_HouseId_Status ON Tenancy(HouseId, Status);
CREATE INDEX IF NOT EXISTS IX_Tenancy_TenantId_Status ON Tenancy(TenantId, Status);
CREATE INDEX IF NOT EXISTS IX_RentCharge_TenancyId ON RentCharge(TenancyId);
CREATE INDEX IF NOT EXISTS IX_RentPayment_RentChargeId ON RentPayment(RentChargeId);
CREATE INDEX IF NOT EXISTS IX_Document_TenantId ON Document(TenantId);
CREATE INDEX IF NOT EXISTS IX_Document_TenancyId ON Document(TenancyId);
CREATE INDEX IF NOT EXISTS IX_Document_Type_IsActive ON Document(Type, IsActive);
CREATE INDEX IF NOT EXISTS IX_Notification_Status_ScheduledFor ON Notification(Status, ScheduledFor);
```

### Running the SQLite Migration

**Using DB Browser for SQLite:**
1. Open `%AppData%\Daryva\Database\DaryvaDB.db` (or create new)
2. Go to "Execute SQL" tab
3. Paste the SQLite schema
4. Click "Execute SQL"

**Using Command Line:**
```bash
# Windows PowerShell
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" < Database\Migrations\001_CreateDatabase_SQLite.sql

# macOS/Linux
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db < Database/Migrations/001_CreateDatabase_SQLite.sql
```

## Functional Testing Checklist

### Application Startup
- [ ] App launches without errors
- [ ] Main window displays correctly
- [ ] No console errors or exceptions
- [ ] Theme loads correctly (Light/Dark)

### Database Operations
- [ ] Database file created in correct location
- [ ] Can create a House
- [ ] Can create a Tenant
- [ ] Can create a Tenancy
- [ ] Can view lists (Houses, Tenants, etc.)
- [ ] Can edit records
- [ ] Can delete records (if implemented)

### File Operations
- [ ] Open file dialog works
- [ ] Save file dialog works
- [ ] Folder browser dialog works
- [ ] Document upload works
- [ ] Document storage path is correct

### Settings
- [ ] Settings can be saved
- [ ] Settings persist after app restart
- [ ] Config files created in correct location
- [ ] Theme switching works

### Exports
- [ ] Export to Excel works
- [ ] Files saved to correct location
- [ ] Export files are valid Excel files

### Backups
- [ ] Backup creation works (SQLite file copy)
- [ ] Backup saved to correct location
- [ ] Backup file is valid SQLite database

### Navigation
- [ ] Navigation between views works
- [ ] Dialogs open/close correctly
- [ ] Data grids display correctly

## Testing Scenarios

### Scenario 1: First Run
1. Delete database file (if exists)
2. Delete config files in AppData
3. Run the app
4. Verify directories are created
5. Verify database file is created
6. Run database migrations
7. Test creating a house

### Scenario 2: Data Operations
1. Create a house
2. Create a tenant
3. Create a tenancy linking them
4. Create a rent charge
5. Record a payment
6. Verify all data persists after restart

### Scenario 3: Cross-Platform
1. Test on Windows
2. Test on macOS (if available)
3. Verify paths are correct on each platform
4. Verify database operations work on both

## Troubleshooting

### App Won't Start

**Check .NET SDK:**
```bash
dotnet --version  # Should show 8.x
```

**Check for errors:**
```bash
dotnet run 2>&1 | tee run.log
```

**Check dependencies:**
```bash
dotnet restore
dotnet list package
```

### Database Errors

**Database file not found:**
- Check if database directory exists
- Check if database file was created
- Verify connection string in config

**SQL syntax errors:**
- Verify all SQL Server syntax was replaced
- Check repository files for remaining SQL Server syntax
- See `MACOS_MIGRATION.md` for syntax conversion table

**Table doesn't exist:**
- Run SQLite migration script
- Verify schema was created correctly
- Check database file with SQLite browser

### Path Issues

**Windows:**
- Check `%AppData%\Daryva\` exists
- Verify write permissions

**macOS:**
- Check `~/Library/Application Support/Daryva/` exists
- Verify write permissions:
  ```bash
  ls -la ~/Library/Application\ Support/Daryva/
  chmod -R u+w ~/Library/Application\ Support/Daryva/  # If needed
  ```

### Configuration Issues

**Config not loading:**
- Check `app.config.json` exists in AppData
- Verify JSON is valid
- Check file permissions

**Connection string issues:**
- Verify SQLite connection string format: `Data Source=path;`
- Check database file path is correct
- Ensure database file is writable

## Performance Testing

### Database Performance
- Test with 100+ houses
- Test with 1000+ tenants
- Test with 10,000+ payments
- Verify queries are fast (< 1 second)

### UI Performance
- Test with large data sets
- Verify scrolling is smooth
- Check memory usage

## Automated Testing (Future)

Consider adding:
- Unit tests for repositories
- Integration tests for services
- UI tests for critical flows

## Next Steps

1. ✅ Convert SQL Server migrations to SQLite
2. ✅ Test all CRUD operations
3. ✅ Test on macOS
4. ✅ Verify all paths work cross-platform
5. ⏳ Add automated tests
6. ⏳ Performance optimization if needed

## Quick Test Commands

### Windows
```powershell
# Build and run
cd Daryva-Avalonia; dotnet run

# Check database
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" ".tables"

# Check logs
Get-Content "$env:APPDATA\Daryva\Logs\*.log" -Tail 50
```

### macOS
```bash
# Build and run
cd Daryva-Avalonia && dotnet run

# Check database
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db ".tables"

# Check logs
tail -50 ~/Library/Application\ Support/Daryva/Logs/*.log
```
