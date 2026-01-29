# Import Data to SQLite - Step by Step

Your migration script ran successfully, but the CSV files were deleted. Here's how to import your data:

## Option 1: Re-run Migration (Keep CSV Files)

```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva"
.\migrate-data-to-sqlite.ps1
```

**Important**: When asked "Delete temporary CSV files? (Y/N)", type **N** (No) to keep them.

Then follow the DB Browser import steps below.

---

## Option 2: Manual Export/Import (Recommended)

### Step 1: Export from SQL Server (SSMS)

1. Open **SQL Server Management Studio (SSMS)**
2. Connect to your **DaryvaDB** database
3. For each table below, run the query and export to CSV:

#### Export Each Table:

**House:**
```sql
SELECT * FROM House
```
- Right-click results → "Save Results As..." → Save as `House.csv`

**Tenant:**
```sql
SELECT * FROM Tenant
```
- Save as `Tenant.csv`

**Tenancy:**
```sql
SELECT * FROM Tenancy
```
- Save as `Tenancy.csv`

**RentCharge:**
```sql
SELECT * FROM RentCharge
```
- Save as `RentCharge.csv`

**RentPayment:**
```sql
SELECT * FROM RentPayment
```
- Save as `RentPayment.csv`

**DepositPayment:**
```sql
SELECT * FROM DepositPayment
```
- Save as `DepositPayment.csv`

**Document:**
```sql
SELECT * FROM Document
```
- Save as `Document.csv`

**HouseExpense:**
```sql
SELECT * FROM HouseExpense
```
- Save as `HouseExpense.csv`

**Notification:**
```sql
SELECT * FROM Notification
```
- Save as `Notification.csv`

**NotificationTemplate:**
```sql
SELECT * FROM NotificationTemplate
```
- Save as `NotificationTemplate.csv`

**NotificationAttempt:**
```sql
SELECT * FROM NotificationAttempt
```
- Save as `NotificationAttempt.csv`

**AppSettings:**
```sql
SELECT * FROM AppSettings
```
- Save as `AppSettings.csv`

---

### Step 2: Import to SQLite (DB Browser)

1. **Open DB Browser for SQLite**
   - If you don't have it: https://sqlitebrowser.org/

2. **Open your database:**
   - File → Open Database
   - Navigate to: `C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db`
   - Click Open

3. **Import each CSV file** (in this order - important for foreign keys):

   **Import Order:**
   1. **House**
      - File → Import → Table from CSV file
      - Select `House.csv`
      - ✅ Check "Column names in first line"
      - Table name: `House`
      - Click OK

   2. **Tenant**
      - File → Import → Table from CSV file
      - Select `Tenant.csv`
      - ✅ Check "Column names in first line"
      - Table name: `Tenant`
      - Click OK

   3. **Tenancy**
      - File → Import → Table from CSV file
      - Select `Tenancy.csv`
      - ✅ Check "Column names in first line"
      - Table name: `Tenancy`
      - Click OK

   4. **RentCharge**
      - File → Import → Table from CSV file
      - Select `RentCharge.csv`
      - ✅ Check "Column names in first line"
      - Table name: `RentCharge`
      - Click OK

   5. **RentPayment**
      - File → Import → Table from CSV file
      - Select `RentPayment.csv`
      - ✅ Check "Column names in first line"
      - Table name: `RentPayment`
      - Click OK

   6. **DepositPayment**
      - File → Import → Table from CSV file
      - Select `DepositPayment.csv`
      - ✅ Check "Column names in first line"
      - Table name: `DepositPayment`
      - Click OK

   7. **Document**
      - File → Import → Table from CSV file
      - Select `Document.csv`
      - ✅ Check "Column names in first line"
      - Table name: `Document`
      - Click OK

   8. **HouseExpense**
      - File → Import → Table from CSV file
      - Select `HouseExpense.csv`
      - ✅ Check "Column names in first line"
      - Table name: `HouseExpense`
      - Click OK

   9. **Notification**
      - File → Import → Table from CSV file
      - Select `Notification.csv`
      - ✅ Check "Column names in first line"
      - Table name: `Notification`
      - Click OK

   10. **NotificationTemplate**
       - File → Import → Table from CSV file
       - Select `NotificationTemplate.csv`
       - ✅ Check "Column names in first line"
       - Table name: `NotificationTemplate`
       - Click OK

   11. **NotificationAttempt**
       - File → Import → Table from CSV file
       - Select `NotificationAttempt.csv`
       - ✅ Check "Column names in first line"
       - Table name: `NotificationAttempt`
       - Click OK

   12. **AppSettings**
       - File → Import → Table from CSV file
       - Select `AppSettings.csv`
       - ✅ Check "Column names in first line"
       - Table name: `AppSettings`
       - Click OK

---

### Step 3: Verify Data

In DB Browser:
1. Click the **"Browse Data"** tab
2. Use the dropdown to select each table
3. Verify row counts match your SQL Server data

**Quick Check:**
- House: Should show your houses
- Tenant: Should show your tenants
- Tenancy: Should show your tenancies
- etc.

---

## Troubleshooting

### "Table already exists" error
- The table might already have data
- You can either:
  - Delete existing data first (Browse Data → Delete rows)
  - Or skip that table if it already has the data

### Foreign key errors
- Make sure you import in the correct order (House → Tenant → Tenancy → etc.)
- Parent tables must be imported before child tables

### Date format issues
- SQLite stores dates as TEXT
- The import should handle this automatically
- If dates look wrong, they might need manual adjustment

### Column mismatch
- Make sure "Column names in first line" is checked
- Verify CSV files have headers
- Check that column names match between SQL Server and SQLite

---

## After Import

Once all data is imported:
1. Save the database (File → Write Changes)
2. Close DB Browser
3. Run your Daryva app - it should now see all your data!

The app is configured to use: `C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db`
