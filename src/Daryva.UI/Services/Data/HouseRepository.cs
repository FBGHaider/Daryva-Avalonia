using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class HouseRepository : IHouseRepository
    {
        private readonly IDbContext _dbContext;

        public HouseRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<House>> GetAllHousesAsync()
        {
            var sql = @"
                WITH CurrentActiveTenancy AS (
                    SELECT t1.TenantId, t1.HouseId, t1.RentAmountMonthly
                    FROM Tenancy t1
                    WHERE t1.Status = 'Active'
                      AND NOT EXISTS (
                          SELECT 1
                          FROM Tenancy t2
                          WHERE t2.TenantId = t1.TenantId
                            AND t2.Status = 'Active'
                            AND (
                                t2.MoveInDate > t1.MoveInDate
                                OR (t2.MoveInDate = t1.MoveInDate AND t2.TenancyId > t1.TenancyId)
                            )
                      )
                )
                SELECT h.*, 
                       (SELECT COUNT(*)
                        FROM CurrentActiveTenancy cat
                        INNER JOIN Tenant tn ON cat.TenantId = tn.TenantId
                        WHERE cat.HouseId = h.HouseId AND tn.IsArchived = 0) AS ActiveTenantCount,
                       (SELECT COALESCE(SUM(cat.RentAmountMonthly), 0)
                        FROM CurrentActiveTenancy cat
                        INNER JOIN Tenant tn ON cat.TenantId = tn.TenantId
                        WHERE cat.HouseId = h.HouseId AND tn.IsArchived = 0) AS TotalMonthlyRent
                FROM House h
                ORDER BY h.CreatedAt DESC";

            return await Task.FromResult(_dbContext.Query<House>(sql));
        }

        public async Task<House?> GetHouseByIdAsync(int houseId)
        {
            var sql = @"
                WITH CurrentActiveTenancy AS (
                    SELECT t1.TenantId, t1.HouseId, t1.RentAmountMonthly
                    FROM Tenancy t1
                    WHERE t1.Status = 'Active'
                      AND NOT EXISTS (
                          SELECT 1
                          FROM Tenancy t2
                          WHERE t2.TenantId = t1.TenantId
                            AND t2.Status = 'Active'
                            AND (
                                t2.MoveInDate > t1.MoveInDate
                                OR (t2.MoveInDate = t1.MoveInDate AND t2.TenancyId > t1.TenancyId)
                            )
                      )
                )
                SELECT h.*, 
                       (SELECT COUNT(*)
                        FROM CurrentActiveTenancy cat
                        INNER JOIN Tenant tn ON cat.TenantId = tn.TenantId
                        WHERE cat.HouseId = h.HouseId AND tn.IsArchived = 0) AS ActiveTenantCount,
                       (SELECT COALESCE(SUM(cat.RentAmountMonthly), 0)
                        FROM CurrentActiveTenancy cat
                        INNER JOIN Tenant tn ON cat.TenantId = tn.TenantId
                        WHERE cat.HouseId = h.HouseId AND tn.IsArchived = 0) AS TotalMonthlyRent
                FROM House h
                WHERE h.HouseId = @HouseId";

            return await Task.FromResult(_dbContext.Query<House>(sql, new { HouseId = houseId }).FirstOrDefault());
        }

        public async Task<int> CreateHouseAsync(House house)
        {
            var sql = @"
                INSERT INTO House (AddressLine1, AddressLine2, City, Postcode, TotalRooms, CreatedAt)
                VALUES (@AddressLine1, @AddressLine2, @City, @Postcode, @TotalRooms, datetime('now'));
                SELECT last_insert_rowid();";

            var houseId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, house));
            return houseId;
        }

        public async Task UpdateHouseAsync(House house)
        {
            var sql = @"
                UPDATE House 
                SET AddressLine1 = @AddressLine1,
                    AddressLine2 = @AddressLine2,
                    City = @City,
                    Postcode = @Postcode,
                    TotalRooms = @TotalRooms
                WHERE HouseId = @HouseId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, house));
        }

        public async Task DeleteHouseAsync(int houseId)
        {
            var sql = "DELETE FROM House WHERE HouseId = @HouseId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { HouseId = houseId }));
        }

        public async Task<IEnumerable<House>> SearchHousesAsync(string searchTerm)
        {
            var sql = @"
                WITH CurrentActiveTenancy AS (
                    SELECT t1.TenantId, t1.HouseId, t1.RentAmountMonthly
                    FROM Tenancy t1
                    WHERE t1.Status = 'Active'
                      AND NOT EXISTS (
                          SELECT 1
                          FROM Tenancy t2
                          WHERE t2.TenantId = t1.TenantId
                            AND t2.Status = 'Active'
                            AND (
                                t2.MoveInDate > t1.MoveInDate
                                OR (t2.MoveInDate = t1.MoveInDate AND t2.TenancyId > t1.TenancyId)
                            )
                      )
                )
                SELECT h.*, 
                       (SELECT COUNT(*)
                        FROM CurrentActiveTenancy cat
                        INNER JOIN Tenant tn ON cat.TenantId = tn.TenantId
                        WHERE cat.HouseId = h.HouseId AND tn.IsArchived = 0) AS ActiveTenantCount,
                       (SELECT COALESCE(SUM(cat.RentAmountMonthly), 0)
                        FROM CurrentActiveTenancy cat
                        INNER JOIN Tenant tn ON cat.TenantId = tn.TenantId
                        WHERE cat.HouseId = h.HouseId AND tn.IsArchived = 0) AS TotalMonthlyRent
                FROM House h
                WHERE h.AddressLine1 LIKE @SearchTerm 
                   OR h.AddressLine2 LIKE @SearchTerm 
                   OR h.City LIKE @SearchTerm 
                   OR h.Postcode LIKE @SearchTerm
                ORDER BY h.CreatedAt DESC";

            return await Task.FromResult(_dbContext.Query<House>(sql, new { SearchTerm = $"%{searchTerm}%" }));
        }
    }
}
