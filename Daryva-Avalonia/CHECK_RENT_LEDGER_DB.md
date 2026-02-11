# How to check the database for Rent Ledger (e.g. "Overdue" when should be "Paid")

## Where is the database?

The app uses a SQLite file. Typical locations:

- **Custom (if set):** `C:\Users\<You>\OneDrive\Documents\DaryvaDB.db`
- **Default:** `%APPDATA%\Daryva\Database\DaryvaDB.db`  
  Full path example: `C:\Users\<You>\AppData\Roaming\Daryva\Database\DaryvaDB.db`
- **If you use app.config.local.json:** open that file and check `ConnectionStrings.DefaultConnection` for the path after `Data Source=`.

Close Daryva before opening the DB with another tool (or use a copy of the file).

---

## Useful SQL queries

Use any SQLite tool (e.g. [DB Browser for SQLite](https://sqlitebrowser.org/), or `sqlite3` in a terminal).

### 1. All rent payments for January 2026

```sql
SELECT rp.RentPaymentId, rp.TenancyId, rp.PaidOn, rp.AmountPaid, rp.RentChargeId,
       tn.FullName AS TenantName
FROM RentPayment rp
LEFT JOIN Tenancy t ON t.TenancyId = rp.TenancyId
LEFT JOIN Tenant tn ON tn.TenantId = t.TenantId
WHERE strftime('%Y', rp.PaidOn) = '2026' AND strftime('%m', rp.PaidOn) = '01'
ORDER BY tn.FullName, rp.PaidOn;
```

If Haider Ali paid in January 2026, you should see a row with his name, that TenancyId, and the amount.

### 2. Rent payments for a tenant by name (e.g. "Haider Ali")

```sql
SELECT rp.RentPaymentId, rp.TenancyId, rp.PaidOn, rp.AmountPaid, rp.RentChargeId,
       tn.FullName, t.Status AS TenancyStatus, t.MoveOutDate
FROM RentPayment rp
JOIN Tenancy t ON t.TenancyId = rp.TenancyId
JOIN Tenant tn ON tn.TenantId = t.TenantId
WHERE tn.FullName LIKE '%Haider%Ali%'
ORDER BY rp.PaidOn DESC;
```

Check that there is a row with `PaidOn` in January 2026. Note the `TenancyId` and `AmountPaid`.

### 3. Rent charges for January 2026 (to compare with ledger rows)

```sql
SELECT rc.RentChargeId, rc.TenancyId, rc.PeriodYear, rc.PeriodMonth, rc.AmountDue, rc.DueDate,
       tn.FullName
FROM RentCharge rc
JOIN Tenancy t ON t.TenancyId = rc.TenancyId
JOIN Tenant tn ON tn.TenantId = t.TenantId
WHERE rc.PeriodYear = 2026 AND rc.PeriodMonth = 1
ORDER BY tn.FullName;
```

Find the row for Haider Ali and note his **TenancyId** for Jan 2026.

### 4. Does the payment’s TenancyId match the charge?

- From query 2 you get Haider’s **TenancyId** on the payment row(s).
- From query 3 you get his **TenancyId** on the charge row for Jan 2026.
- If they differ (e.g. two tenancy records for the same person), the app now also sums by **TenantId**, so it should still show Paid. If the payment is missing in query 1, the problem is usually:
  - `PaidOn` not in January 2026 (wrong month/year), or
  - `PaidOn` stored in a format SQLite’s `strftime('%Y', PaidOn)` doesn’t read (try `SELECT PaidOn, strftime('%Y', PaidOn), strftime('%m', PaidOn) FROM RentPayment LIMIT 5;` to check).

---

## Run SQL from command line (if sqlite3 is installed)

```bash
# Replace path with your actual DB path
sqlite3 "%APPDATA%\Daryva\Database\DaryvaDB.db" "SELECT rp.RentPaymentId, rp.TenancyId, rp.PaidOn, rp.AmountPaid, tn.FullName FROM RentPayment rp JOIN Tenancy t ON t.TenancyId = rp.TenancyId JOIN Tenant tn ON tn.TenantId = t.TenantId WHERE tn.FullName LIKE '%Haider%';"
```
