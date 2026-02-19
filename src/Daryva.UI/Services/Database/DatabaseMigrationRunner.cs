using System.Linq;
using System.Text;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Runs database migrations on startup. Migrations are idempotent where possible.
    /// When the database is empty (e.g. new folder from installer), creates full schema first.
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
                RunInitialSchemaIfNeeded();
                RunMigration016_RentStartAndBackfillMoveIn();
                RunMigration017_EndDuplicateActiveTenancies();
                RunMigration018_PaidFromDeposit();
                RunMigration019_DepositReturn();
                RunMigration020_DocumentHouseIdNullable();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
                // Don't rethrow - allow app to continue; TenancyRepository will fall back to query without new columns
            }
        }

        /// <summary>
        /// If the database has no tables (e.g. new empty DB from installer-chosen folder), run the full initial schema.
        /// </summary>
        private void RunInitialSchemaIfNeeded()
        {
            if (TableExists("House"))
                return;

            var schema = GetInitialSchemaSql();
            foreach (var statement in SplitSqlStatements(schema))
            {
                if (string.IsNullOrWhiteSpace(statement))
                    continue;
                try
                {
                    _dbContext.ExecuteNonQuery(statement);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Initial schema statement failed: {ex.Message}");
                }
            }
        }

        private static bool TableExists(IDbContext db, string tableName)
        {
            try
            {
                var sql = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@Name";
                var value = db.ExecuteScalar<int?>(sql, new { Name = tableName });
                return value.HasValue;
            }
            catch
            {
                return false;
            }
        }

        private bool TableExists(string tableName) => TableExists(_dbContext, tableName);

        private static IEnumerable<string> SplitSqlStatements(string sql)
        {
            var sb = new StringBuilder();
            foreach (var line in sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("--", StringComparison.Ordinal))
                    continue;
                sb.AppendLine(trimmed);
            }
            return sb.ToString()
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);
        }

        private static string GetInitialSchemaSql() => @"
CREATE TABLE IF NOT EXISTS House (
    HouseId INTEGER PRIMARY KEY AUTOINCREMENT,
    AddressLine1 TEXT NOT NULL,
    AddressLine2 TEXT,
    City TEXT NOT NULL,
    Postcode TEXT NOT NULL,
    TotalRooms INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE TABLE IF NOT EXISTS Tenant (
    TenantId INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName TEXT NOT NULL,
    PhoneNumber TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE,
    UniversityName TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    IsArchived INTEGER NOT NULL DEFAULT 0
);
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
CREATE TABLE IF NOT EXISTS Document (
    DocumentId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenantId INTEGER,
    TenancyId INTEGER,
    HouseId INTEGER,
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
CREATE TABLE IF NOT EXISTS NotificationAttempt (
    AttemptId INTEGER PRIMARY KEY AUTOINCREMENT,
    NotificationId INTEGER NOT NULL,
    AttemptedAt TEXT NOT NULL,
    Status TEXT NOT NULL,
    Error TEXT,
    ProviderMessageId TEXT,
    FOREIGN KEY (NotificationId) REFERENCES Notification(NotificationId)
);
CREATE TABLE IF NOT EXISTS AppSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT,
    SettingType TEXT NOT NULL DEFAULT 'String',
    Category TEXT NOT NULL DEFAULT 'General',
    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);
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
";

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

        /// <summary>Makes Document.HouseId nullable so tenant-only documents can be stored.</summary>
        private void RunMigration020_DocumentHouseIdNullable()
        {
            if (!TableExists("Document"))
                return;
            try
            {
                var sql = "PRAGMA table_info(Document)";
                var rows = _dbContext.Query<TableInfoRow>(sql) ?? Enumerable.Empty<TableInfoRow>();
                var houseIdCol = rows.FirstOrDefault(r => r != null && string.Equals(r.name, "HouseId", StringComparison.OrdinalIgnoreCase));
                if (houseIdCol == null || houseIdCol.notnull == 0)
                    return; // Already nullable or no column

                // If a previous run left Document_new behind, remove it
                if (TableExists("Document_new"))
                    _dbContext.ExecuteNonQuery("DROP TABLE Document_new");

                // Disable foreign keys so we can drop Document (HouseExpense references it)
                _dbContext.ExecuteNonQuery("PRAGMA foreign_keys = OFF");

                try
                {
                    _dbContext.ExecuteNonQuery(@"
CREATE TABLE Document_new (
    DocumentId INTEGER PRIMARY KEY AUTOINCREMENT,
    TenantId INTEGER,
    TenancyId INTEGER,
    HouseId INTEGER,
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
)");
                    _dbContext.ExecuteNonQuery(@"
INSERT INTO Document_new (DocumentId, TenantId, TenancyId, HouseId, Type, FileName, StoragePath, FileMimeType, Version, IsActive, UploadedAt, DisplayName, Source)
SELECT DocumentId, TenantId, TenancyId, HouseId, Type, FileName, StoragePath, FileMimeType, Version, IsActive, UploadedAt, DisplayName, Source FROM Document");
                    _dbContext.ExecuteNonQuery("DROP TABLE Document");
                    _dbContext.ExecuteNonQuery("ALTER TABLE Document_new RENAME TO Document");
                    // Recreate indexes
                    _dbContext.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS IX_Document_TenantId ON Document(TenantId)");
                    _dbContext.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS IX_Document_TenancyId ON Document(TenancyId)");
                    _dbContext.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS IX_Document_HouseId ON Document(HouseId)");
                    _dbContext.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS IX_Document_Type_IsActive ON Document(Type, IsActive)");
                }
                finally
                {
                    _dbContext.ExecuteNonQuery("PRAGMA foreign_keys = ON");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration 020 DocumentHouseIdNullable: {ex.Message}");
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
