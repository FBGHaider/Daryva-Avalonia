using Dapper;
using LandLordBuddy.MVVM.Models;
using LandLordBuddy.Services.Database;

namespace LandLordBuddy.Services.Data
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
                SELECT t.*, h.*, tn.*
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.HouseId = @HouseId
                ORDER BY t.MoveInDate DESC";

            _dbContext.OpenConnection();
            // Materialize immediately to close DataReader
            var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sql,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { HouseId = houseId },
                splitOn: "HouseId,TenantId").ToList();
            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<Tenancy>> GetTenanciesByTenantIdAsync(int tenantId)
        {
            var sql = @"
                SELECT t.*, h.*, tn.*
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.TenantId = @TenantId
                ORDER BY t.MoveInDate DESC";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sql,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { TenantId = tenantId },
                splitOn: "HouseId,TenantId").ToList();
            return await Task.FromResult(results);
        }

        public async Task<Tenancy?> GetTenancyByIdAsync(int tenancyId)
        {
            var sql = @"
                SELECT t.*, h.*, tn.*
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.TenancyId = @TenancyId";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sql,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                new { TenancyId = tenancyId },
                splitOn: "HouseId,TenantId").ToList();

            return await Task.FromResult(results.FirstOrDefault());
        }

        public async Task<IEnumerable<Tenancy>> GetActiveTenanciesAsync()
        {
            var sql = @"
                SELECT t.*, h.*, tn.*
                FROM Tenancy t
                INNER JOIN House h ON t.HouseId = h.HouseId
                INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                WHERE t.Status = 'Active' AND tn.IsArchived = 0
                ORDER BY t.MoveInDate DESC";

            _dbContext.OpenConnection();
            // Materialize the query result to close DataReader
            var results = _dbContext.Connection.Query<Tenancy, House, Tenant, Tenancy>(sql,
                (tenancy, house, tenant) =>
                {
                    tenancy.House = house;
                    tenancy.Tenant = tenant;
                    return tenancy;
                },
                splitOn: "HouseId,TenantId").ToList();
            return await Task.FromResult(results);
        }

        public async Task<int> CreateTenancyAsync(Tenancy tenancy)
        {
            var sql = @"
                INSERT INTO Tenancy (HouseId, TenantId, MoveInDate, MoveOutDate, RentAmountMonthly, DepositAmount, PaymentDueDay, Status, Notes)
                VALUES (@HouseId, @TenantId, @MoveInDate, @MoveOutDate, @RentAmountMonthly, @DepositAmount, @PaymentDueDay, @Status, @Notes);
                SELECT CAST(SCOPE_IDENTITY() as int);";

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
    }
}
