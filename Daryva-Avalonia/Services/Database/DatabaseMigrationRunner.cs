using System.Linq;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Runs database migrations on startup. Migrations are idempotent where possible.
    /// </summary>
    public class DatabaseMigrationRunner
    {
        private readonly IDbContext _dbContext;

        public DatabaseMigrationRunner(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void RunMigrations()
        {
            try
            {
                RunMigration016_RentStartAndBackfillMoveIn();
                RunMigration017_EndDuplicateActiveTenancies();
                RunMigration018_PaidFromDeposit();
                RunMigration019_DepositReturn();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
                // Don't rethrow - allow app to continue; TenancyRepository will fall back to query without new columns
            }
        }

        private void RunMigration016_RentStartAndBackfillMoveIn()
        {
            if (HasColumn("Tenancy", "RentStartMonth"))
                return;

            _dbContext.ExecuteNonQuery("ALTER TABLE Tenancy ADD COLUMN RentStartMonth INTEGER");
            _dbContext.ExecuteNonQuery("ALTER TABLE Tenancy ADD COLUMN RentStartYear INTEGER");

            _dbContext.ExecuteNonQuery(@"
                UPDATE Tenancy 
                SET MoveInDate = (SELECT MIN(PaidOn) FROM RentPayment WHERE RentPayment.TenancyId = Tenancy.TenancyId)
                WHERE TenancyId IN (SELECT TenancyId FROM RentPayment)");

            _dbContext.ExecuteNonQuery(@"
                UPDATE Tenancy 
                SET MoveInDate = (SELECT MIN(PaidOn) FROM DepositPayment WHERE DepositPayment.TenancyId = Tenancy.TenancyId)
                WHERE TenancyId IN (SELECT TenancyId FROM DepositPayment) 
                  AND TenancyId NOT IN (SELECT TenancyId FROM RentPayment)");

            // Backfill RentStartMonth/Year for tenancies where both null (default to same month as move-in)
            _dbContext.ExecuteNonQuery(@"
                UPDATE Tenancy 
                SET RentStartMonth = CAST(strftime('%m', MoveInDate) AS INTEGER),
                    RentStartYear = CAST(strftime('%Y', MoveInDate) AS INTEGER)
                WHERE RentStartMonth IS NULL AND RentStartYear IS NULL");
        }

        private void RunMigration018_PaidFromDeposit()
        {
            if (HasColumn("RentPayment", "PaidFromDeposit"))
                return;
            _dbContext.ExecuteNonQuery("ALTER TABLE RentPayment ADD COLUMN PaidFromDeposit INTEGER NOT NULL DEFAULT 0");
        }

        private void RunMigration019_DepositReturn()
        {
            _dbContext.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS DepositReturn (
                    DepositReturnId INTEGER PRIMARY KEY AUTOINCREMENT,
                    TenancyId INTEGER NOT NULL,
                    ReturnedDate TEXT NOT NULL,
                    AmountReturned REAL NOT NULL,
                    Notes TEXT,
                    FOREIGN KEY (TenancyId) REFERENCES Tenancy(TenancyId)
                )");
        }

        private void RunMigration017_EndDuplicateActiveTenancies()
        {
            // End duplicate active tenancies (same TenantId + HouseId) - keep most recent MoveInDate
            var sql = @"
                SELECT TenancyId, TenantId, HouseId, MoveInDate 
                FROM Tenancy 
                WHERE Status = 'Active' AND (MoveOutDate IS NULL OR MoveOutDate = '')";
            var tenancies = _dbContext.Query<TenancyRow>(sql)?.ToList() ?? new List<TenancyRow>();
            var toEnd = tenancies
                .GroupBy(t => new { t.TenantId, t.HouseId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderByDescending(t => t.MoveInDate).Skip(1))
                .Select(t => t.TenancyId)
                .ToList();
            foreach (var id in toEnd)
            {
                _dbContext.ExecuteNonQuery(
                    "UPDATE Tenancy SET MoveOutDate = date('now'), Status = 'Ended' WHERE TenancyId = @TenancyId",
                    new { TenancyId = id });
            }
        }

        private bool HasColumn(string tableName, string columnName)
        {
            try
            {
                var sql = $"PRAGMA table_info({tableName})";
                var rows = _dbContext.Query<TableInfoRow>(sql) ?? Enumerable.Empty<TableInfoRow>();
                return rows.Any(r => r != null && string.Equals(r.name, columnName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private class TableInfoRow
        {
            public int cid { get; set; }
            public string? name { get; set; }
            public string? type { get; set; }
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }

        private class TenancyRow
        {
            public int TenancyId { get; set; }
            public int TenantId { get; set; }
            public int HouseId { get; set; }
            public DateTime MoveInDate { get; set; }
        }
    }
}
