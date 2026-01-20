# LandLord Buddy Database Schema

This folder contains SQL migration scripts to set up the LandLord Buddy database schema in SQL Server.

## Database Structure

The database includes the following main entities:

### Core Entities
- **House** - Property information
- **Tenant** - Tenant information
- **Tenancy** - Relationship between tenants and houses (move-in/out dates, rent, deposit)

### Rent Tracking
- **RentCharge** - Monthly rent charges per tenancy
- **RentPayment** - Payments made against rent charges

### Document Management
- **Document** - Stores metadata for documents (contracts, student letters, IDs, etc.)

### House Expenses
- **HouseExpense** - Tracks expenses for each house

### Notifications
- **Notification** - Tracks email/WhatsApp notifications sent to tenants

### Contract Terms (Optional)
- **TenancyRule** - Stores contract clauses and rules

## Setup Instructions

### Prerequisites
1. Docker SQL Server container running (see main README-Docker.md)
2. SQL Server Management Studio (SSMS) or Azure Data Studio

### Running Migrations

1. **Connect to SQL Server:**
   - Server: `localhost,1433`
   - Authentication: SQL Server Authentication
   - Username: `sa`
   - Password: `image.png`

2. **Create the database (if not exists):**
   ```sql
   CREATE DATABASE LandLordBuddyDB;
   GO
   ```

3. **Run migrations in order:**
   - Execute `001_CreateDatabase.sql` - Creates all tables
   - Execute `002_CreateIndexes.sql` - Creates indexes for performance
   - Execute `003_CreateViewsAndFunctions.sql` - Creates helpful views and functions

### Migration Files

- **001_CreateDatabase.sql** - Creates all tables with foreign keys and constraints
- **002_CreateIndexes.sql** - Creates performance indexes as specified in the design
- **003_CreateViewsAndFunctions.sql** - Creates views and helper functions for common queries

## Key Features

### Indexes
- Indexes on `(HouseId, Status)` and `(TenantId, Status)` for Tenancy queries
- Unique constraint on `(TenancyId, PeriodYear, PeriodMonth)` for RentCharge
- Indexes on Document lookups by TenancyId/TenantId, Type, and IsActive
- Index on Notification `(Status, ScheduledFor)` for pending notifications

### Views
- `vw_ActiveTenancies` - Active tenancies with tenant and house info
- `vw_HouseSummary` - House summary with active tenant count (calculated)
- `vw_CurrentMonthRentStatus` - Current month rent status with payment details

### Functions
- `fn_GetActiveTenantCount(@HouseId)` - Returns active tenant count for a house
- `fn_IsRentPaid(@TenancyId, @PeriodYear, @PeriodMonth)` - Checks if rent is paid for a period

## Connection String

The connection string in `App.config` is already configured:
```
Server=localhost,1433;Database=LandLordBuddyDB;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;Encrypt=True;
```

## Notes

- All timestamps use `DATETIME2` with UTC defaults
- Decimal amounts use `DECIMAL(10,2)` for currency
- Status fields use CHECK constraints for data integrity
- Foreign keys are set to `ON DELETE NO ACTION` to prevent accidental data loss
- Documents can be linked to Tenant, Tenancy, or House (at least one required)
- RentCharge has a unique constraint to prevent duplicate charges for the same period

## Next Steps

After running the migrations:
1. Test the connection from your application
2. Create sample data for testing
3. Use the views and functions in your application queries
