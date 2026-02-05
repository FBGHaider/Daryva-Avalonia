using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class TenancyRepository : ITenancyRepository
    {
        private readonly IDbContext _dbContext;

        public TenancyRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Tenancy>> GetTenanciesByHouseIdAsync(int houseId)
        {
            var sql = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentStartMonth, t.RentStartYear,
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.HouseId = @HouseId
                ORDER BY t.MoveInDate DESC";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sql,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { HouseId = houseId },
                splitOn: "House_HouseId,Tenant_TenantId").ToList();
            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<Tenancy>> GetTenanciesByTenantIdAsync(int tenantId)
        {
            return await GetTenanciesByTenantIdWithOptionalRentStartAsync(tenantId);
        }

        private async Task<IEnumerable<Tenancy>> GetTenanciesByTenantIdWithOptionalRentStartAsync(int tenantId)
        {
            var sqlWithRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentStartMonth, t.RentStartYear,
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.TenantId = @TenantId
                ORDER BY t.MoveInDate DESC";

            var sqlWithoutRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.TenantId = @TenantId
                ORDER BY t.MoveInDate DESC";

            try
            {
                _dbContext.OpenConnection();
                var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithRentStart,
                    (tenancy, house, tenant) =>
                    {
                        tenancy.House = house;
                        tenancy.Tenant = tenant;
                        return tenancy;
                    },
                    new { TenantId = tenantId },
                    splitOn: "House_HouseId,Tenant_TenantId").ToList();
                return await Task.FromResult(results);
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
            }
            catch (NullReferenceException)
            {
            }

            _dbContext.OpenConnection();
            var fallbackResults = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithoutRentStart,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { TenantId = tenantId },
                splitOn: "House_HouseId,Tenant_TenantId").ToList();
            return await Task.FromResult(fallbackResults);
        }

        public async Task<Tenancy?> GetTenancyByIdAsync(int tenancyId)
        {
            var sqlWithRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentStartMonth, t.RentStartYear,
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.TenancyId = @TenancyId";

            var sqlWithoutRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.TenancyId = @TenancyId";

            try
            {
                _dbContext.OpenConnection();
                var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithRentStart,
                    (tenancy, house, tenant) =>
                    {
                        tenancy.House = house;
                        tenancy.Tenant = tenant;
                        return tenancy;
                    },
                    new { TenancyId = tenancyId },
                    splitOn: "House_HouseId,Tenant_TenantId").ToList();
                return await Task.FromResult(results.FirstOrDefault());
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
            }
            catch (NullReferenceException)
            {
            }

            _dbContext.OpenConnection();
            var fallbackResults = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithoutRentStart,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { TenancyId = tenancyId },
                splitOn: "House_HouseId,Tenant_TenantId").ToList();
            return await Task.FromResult(fallbackResults.FirstOrDefault());
        }

        public async Task<IEnumerable<Tenancy>> GetActiveTenanciesAsync()
        {
            return await GetActiveTenanciesWithOptionalRentStartAsync();
        }

        public async Task<IEnumerable<Tenancy>> GetTenanciesActiveInPeriodAsync(int year, int month)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var periodStartStr = periodStart.ToString("yyyy-MM-dd");
            var periodEndStr = periodEnd.ToString("yyyy-MM-dd");
            var sqlWithRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentStartMonth, t.RentStartYear,
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE date(t.MoveInDate) <= @PeriodEnd
                  AND (t.MoveOutDate IS NULL OR date(t.MoveOutDate) >= @PeriodStart)
                ORDER BY t.MoveInDate DESC";
            var sqlWithoutRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE date(t.MoveInDate) <= @PeriodEnd
                  AND (t.MoveOutDate IS NULL OR date(t.MoveOutDate) >= @PeriodStart)
                ORDER BY t.MoveInDate DESC";
            try
            {
                _dbContext.OpenConnection();
                var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithRentStart,
                    (tenancy, house, tenant) =>
                    {
                        tenancy.House = house;
                        tenancy.Tenant = tenant;
                        return tenancy;
                    },
                    new { PeriodStart = periodStartStr, PeriodEnd = periodEndStr },
                    splitOn: "House_HouseId,Tenant_TenantId").ToList();
                return await Task.FromResult(results);
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
            }
            catch (NullReferenceException)
            {
            }
            _dbContext.OpenConnection();
            var fallback = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithoutRentStart,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { PeriodStart = periodStartStr, PeriodEnd = periodEndStr },
                splitOn: "House_HouseId,Tenant_TenantId").ToList();
            return await Task.FromResult(fallback);
        }

        public Task<Dictionary<int, (int Month, int Year)>> GetRentStartByTenancyIdsAsync(IEnumerable<int> tenancyIds)
        {
            var ids = tenancyIds.Distinct().ToList();
            if (ids.Count == 0) return Task.FromResult(new Dictionary<int, (int, int)>());

            var dict = new Dictionary<int, (int, int)>();
            try
            {
                // Query one by one to avoid IN-clause issues across SQLite/Dapper; still single round-trip per ID
                foreach (var id in ids)
                {
                    var sql = "SELECT TenancyId, RentStartMonth, RentStartYear FROM Tenancy WHERE TenancyId = @TenancyId";
                    var row = _dbContext.Query<RentStartRow>(sql, new { TenancyId = id }).FirstOrDefault();
                    if (row != null && row.RentStartMonth.HasValue && row.RentStartMonth.Value >= 1 && row.RentStartMonth.Value <= 12 &&
                        row.RentStartYear.HasValue && row.RentStartYear.Value >= 2000 && row.RentStartYear.Value <= 2100)
                        dict[id] = (row.RentStartMonth.Value, row.RentStartYear.Value);
                }
            }
            catch
            {
                // ignore
            }
            return Task.FromResult(dict);
        }

        private class RentStartRow
        {
            public int TenancyId { get; set; }
            public int? RentStartMonth { get; set; }
            public int? RentStartYear { get; set; }
            public DateTime MoveInDate { get; set; }
        }

        private async Task<IEnumerable<Tenancy>> GetActiveTenanciesWithOptionalRentStartAsync()
        {
            // Use aliases for split columns so Dapper splits at House/Tenant tables, not Tenancy columns
            var sqlWithRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentStartMonth, t.RentStartYear,
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.Status = 'Active' AND tn.IsArchived = 0
                ORDER BY t.MoveInDate DESC";

            var sqlWithoutRentStart = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.Status = 'Active' AND tn.IsArchived = 0
                ORDER BY t.MoveInDate DESC";

            try
            {
                _dbContext.OpenConnection();
                var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithRentStart,
                    (tenancy, house, tenant) =>
                    {
                        tenancy.House = house;
                        tenancy.Tenant = tenant;
                        return tenancy;
                    },
                    splitOn: "House_HouseId,Tenant_TenantId").ToList();
                return await Task.FromResult(results);
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // Columns may not exist if migration hasn't run - fall back to query without RentStartMonth/Year
            }
            catch (NullReferenceException)
            {
                // Defensive: handle potential null ref from Dapper mapping when columns missing
            }

            _dbContext.OpenConnection();
            var fallbackResults = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sqlWithoutRentStart,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                splitOn: "House_HouseId,Tenant_TenantId").ToList();
            return await Task.FromResult(fallbackResults);
        }

        public async Task<int> CreateTenancyAsync(Tenancy tenancy)
        {
            var sql = @"
                INSERT INTO Tenancy (HouseId, TenantId, MoveInDate, MoveOutDate, RentStartMonth, RentStartYear, RentAmountMonthly, DepositAmount, PaymentDueDay, Status, Notes)
                VALUES (@HouseId, @TenantId, @MoveInDate, @MoveOutDate, @RentStartMonth, @RentStartYear, @RentAmountMonthly, @DepositAmount, @PaymentDueDay, @Status, @Notes);
                SELECT last_insert_rowid();";

            var tenancyId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, tenancy));
            return tenancyId;
        }

        public async Task UpdateTenancyAsync(Tenancy tenancy)
        {
            var sql = @"
                UPDATE Tenancy 
                SET HouseId = @HouseId,
                    TenantId = @TenantId,
                    MoveInDate = @MoveInDate,
                    MoveOutDate = @MoveOutDate,
                    RentStartMonth = @RentStartMonth,
                    RentStartYear = @RentStartYear,
                    RentAmountMonthly = @RentAmountMonthly,
                    DepositAmount = @DepositAmount,
                    PaymentDueDay = @PaymentDueDay,
                    Status = @Status,
                    Notes = @Notes
                WHERE TenancyId = @TenancyId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, tenancy));
        }

        public async Task EndTenancyAsync(int tenancyId, DateTime moveOutDate)
        {
            var sql = @"
                UPDATE Tenancy 
                SET MoveOutDate = @MoveOutDate,
                    Status = 'Ended'
                WHERE TenancyId = @TenancyId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenancyId = tenancyId, MoveOutDate = moveOutDate }));
        }

        public async Task ReactivateTenancyAsync(int tenancyId)
        {
            var sql = @"
                UPDATE Tenancy 
                SET MoveOutDate = NULL,
                    Status = 'Active'
                WHERE TenancyId = @TenancyId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenancyId = tenancyId }));
        }

        public async Task<IEnumerable<Tenancy>> GetEndedTenanciesWithDepositAsync()
        {
            var sql = @"
                SELECT 
                    t.TenancyId, t.HouseId, t.TenantId, t.MoveInDate, t.MoveOutDate, 
                    t.RentStartMonth, t.RentStartYear,
                    t.RentAmountMonthly, t.DepositAmount, t.PaymentDueDay, t.Status, t.Notes,
                    h.HouseId AS House_HouseId, h.HouseId AS HouseId, h.AddressLine1, h.AddressLine2, h.City, h.Postcode, h.TotalRooms, h.CreatedAt,
                    tn.TenantId AS Tenant_TenantId, tn.TenantId AS TenantId, tn.FullName, tn.PhoneNumber, tn.Email, tn.UniversityName, tn.CreatedAt, tn.IsArchived
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.Status = 'Ended' AND t.DepositAmount > 0 AND t.MoveOutDate IS NOT NULL
                ORDER BY t.MoveOutDate DESC";
            try
            {
                _dbContext.OpenConnection();
                var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sql,
                    (tenancy, house, tenant) =>
                    {
                        tenancy.House = house;
                        tenancy.Tenant = tenant;
                        return tenancy;
                    },
                    splitOn: "House_HouseId,Tenant_TenantId").ToList();
                return await Task.FromResult(results);
            }
            catch
            {
                return Array.Empty<Tenancy>();
            }
        }

        public async Task DeleteTenancyAsync(int tenancyId)
        {
            var sql = "DELETE FROM Tenancy WHERE TenancyId = @TenancyId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenancyId = tenancyId }));
        }

        public async Task DeleteEndedTenanciesByHouseIdAsync(int houseId)
        {
            var sql = "DELETE FROM Tenancy WHERE HouseId = @HouseId AND Status = 'Ended'";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { HouseId = houseId }));
        }
    }
}
