# Quick Data Migration Guide

Your SQLite database is located at:
**`C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db`**

## Quick Migration Steps

### Option 1: Use the PowerShell Script

```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva"
.\migrate-data-to-sqlite.ps1
```

The script is already configured to use your database location.

### Option 2: Manual Migration (SSMS + DB Browser)

#### Step 1: Export from SQL Server (SSMS)

1. Open SQL Server Management Studio
2. Connect to your DaryvaDB database
3. For each table, run:
   ```sql
   SELECT * FROM House
   ```
4. Right-click results → "Save Results As..." → Save as CSV
5. Repeat for all tables:
   - House
   - Tenant
   - Tenancy
   - RentCharge
   - RentPayment
   - DepositPayment
   - Document
   - HouseExpense
   - Notification
   - NotificationTemplate
   - NotificationAttempt
   - AppSettings

#### Step 2: Import to SQLite (DB Browser)

1. Open DB Browser for SQLite
2. Open database: `C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db`
3. For each CSV file:
   - File → Import → Table from CSV file
   - Select your CSV file
   - **Important**: Check "Column names in first line"
   - Click OK

**Import Order** (respects foreign keys):
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

### Step 3: Verify Data

In DB Browser:
- Click "Browse Data" tab
- Select each table from dropdown
- Verify row counts match your SQL Server data

Or use command line:
```powershell
sqlite3 "C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db" "SELECT COUNT(*) FROM House;"
sqlite3 "C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db" "SELECT COUNT(*) FROM Tenant;"
```

## Configuration

The app has been updated to automatically use your database location:
- `C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db`

If the database is not found there, it will fall back to:
- `%AppData%\Daryva\Database\DaryvaDB.db`

## Troubleshooting

### Database file not found
- Verify the path: `C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db`
- Make sure the file exists (or create it first using DB Browser)

### Import errors
- Make sure you imported tables in the correct order (respects foreign keys)
- Check that CSV files have headers
- Verify data types match (dates, numbers, etc.)

### Connection errors
- Check the database file path in the app's configuration
- Verify the database file is not locked (close DB Browser if open)
