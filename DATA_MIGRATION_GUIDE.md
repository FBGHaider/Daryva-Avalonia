# Data Migration Guide: SQL Server to SQLite

Guide to migrate your existing SQL Server data to the new SQLite database.

## Method 1: Using SQL Server Management Studio (SSMS) - Recommended

### Step 1: Export Data from SQL Server

1. **Open SQL Server Management Studio (SSMS)**
2. **Connect to your SQL Server database** (DaryvaDB)
3. **Right-click on your database** → Tasks → Export Data...
4. **Choose export method:**
   - For small databases: Export to CSV, then import to SQLite
   - For larger databases: Use the script method below

### Step 2: Export Each Table to CSV

For each table, run this in SSMS:

```sql
-- Example: Export House table
SELECT * FROM House
```

1. Right-click the results → "Save Results As..."
2. Save as CSV (e.g., `House.csv`)
3. Repeat for all tables

### Step 3: Import to SQLite using DB Browser

1. Open DB Browser for SQLite
2. Open your SQLite database: `%AppData%\Daryva\Database\DaryvaDB.db`
3. For each CSV file:
   - Go to "File" → "Import" → "Table from CSV file"
   - Select your CSV file
   - Map columns if needed
   - Click "OK"

---

## Method 2: Using PowerShell Script (Automated)

I'll create a PowerShell script that:
1. Connects to SQL Server
2. Exports all data
3. Converts to SQLite format
4. Imports into SQLite

---

## Method 3: Manual SQL Export/Import

### Step 1: Generate INSERT Statements from SQL Server

For each table, run this in SSMS:

```sql
-- Example for House table
SELECT 
    'INSERT INTO House (AddressLine1, AddressLine2, City, Postcode, TotalRooms, CreatedAt) VALUES ('
    + '''' + REPLACE(AddressLine1, '''', '''''') + ''', '
    + ISNULL('''' + REPLACE(AddressLine2, '''', '''''') + '''', 'NULL') + ', '
    + '''' + REPLACE(City, '''', '''''') + ''', '
    + '''' + REPLACE(Postcode, '''', '''''') + ''', '
    + CAST(TotalRooms AS VARCHAR) + ', '
    + '''' + CONVERT(VARCHAR, CreatedAt, 120) + ''');'
FROM House;
```

### Step 2: Copy and Run in SQLite

1. Copy the generated INSERT statements
2. Paste into DB Browser's "Execute SQL" tab
3. Execute

---

## Method 4: Using sqlite3 Command Line (Advanced)

### Export from SQL Server to CSV

```powershell
# Export each table
sqlcmd -S localhost -d DaryvaDB -E -Q "SELECT * FROM House" -o House.csv -s "," -W
sqlcmd -S localhost -d DaryvaDB -E -Q "SELECT * FROM Tenant" -o Tenant.csv -s "," -W
# ... repeat for all tables
```

### Import to SQLite

```powershell
# Import each CSV
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" <<EOF
.mode csv
.import House.csv House
.import Tenant.csv Tenant
# ... repeat for all tables
EOF
```

---

## Important Notes

### Data Type Conversions

- **Dates**: SQL Server `DATETIME2` → SQLite `TEXT` (format: `YYYY-MM-DD HH:MM:SS`)
- **Booleans**: SQL Server `BIT` (0/1) → SQLite `INTEGER` (0/1) - same!
- **Decimals**: SQL Server `DECIMAL` → SQLite `REAL` - should work fine
- **Strings**: SQL Server `NVARCHAR` → SQLite `TEXT` - same!

### Foreign Key Order

Import tables in this order to respect foreign keys:
1. House
2. Tenant
3. Tenancy
4. RentCharge
5. RentPayment
6. DepositPayment
7. Document
8. HouseExpense
9. Notification
10. NotificationTemplate
11. NotificationAttempt
12. AppSettings

### Identity Columns

SQL Server uses `IDENTITY(1,1)`, SQLite uses `AUTOINCREMENT`. When importing:
- **Option A**: Let SQLite auto-generate new IDs (simpler, but IDs will change)
- **Option B**: Preserve original IDs (more complex, requires disabling auto-increment temporarily)

---

## Quick Migration Script

I'll create a PowerShell script to automate this process.
