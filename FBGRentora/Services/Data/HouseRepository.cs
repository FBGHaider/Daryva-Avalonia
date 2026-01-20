using Dapper;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Database;

namespace FBGRentora.Services.Data
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
                SELECT h.*, 
                       (SELECT COUNT(DISTINCT t.TenantId) 
                        FROM Tenancy t 
                        INNER JOIN Tenant tn ON t.TenantId = tn.TenantId 
                        WHERE t.HouseId = h.HouseId AND t.Status = 'Active' AND tn.IsArchived = 0) AS ActiveTenantCount,
                       -- Sum only monthly rent, NOT deposit amount
                       -- Only include tenancies for non-archived tenants
                       (SELECT ISNULL(SUM(t.RentAmountMonthly), 0) 
                        FROM Tenancy t 
                        INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                        WHERE t.HouseId = h.HouseId AND t.Status = 'Active' AND tn.IsArchived = 0) AS TotalMonthlyRent
                FROM House h
                ORDER BY h.CreatedAt DESC";

            return await Task.FromResult(_dbContext.Query<House>(sql));
        }

        public async Task<House?> GetHouseByIdAsync(int houseId)
        {
            var sql = @"
                SELECT h.*, 
                       (SELECT COUNT(DISTINCT t.TenantId) 
                        FROM Tenancy t 
                        INNER JOIN Tenant tn ON t.TenantId = tn.TenantId 
                        WHERE t.HouseId = h.HouseId AND t.Status = 'Active' AND tn.IsArchived = 0) AS ActiveTenantCount,
                       -- Sum only monthly rent, NOT deposit amount
                       -- Only include tenancies for non-archived tenants
                       (SELECT ISNULL(SUM(t.RentAmountMonthly), 0) 
                        FROM Tenancy t 
                        INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                        WHERE t.HouseId = h.HouseId AND t.Status = 'Active' AND tn.IsArchived = 0) AS TotalMonthlyRent
                FROM House h
                WHERE h.HouseId = @HouseId";

            return await Task.FromResult(_dbContext.Query<House>(sql, new { HouseId = houseId }).FirstOrDefault());
        }

        public async Task<int> CreateHouseAsync(House house)
        {
            var sql = @"
                INSERT INTO House (AddressLine1, AddressLine2, City, Postcode, TotalRooms, CreatedAt)
                VALUES (@AddressLine1, @AddressLine2, @City, @Postcode, @TotalRooms, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() as int);";

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
                SELECT h.*, 
                       (SELECT COUNT(DISTINCT t.TenantId) 
                        FROM Tenancy t 
                        INNER JOIN Tenant tn ON t.TenantId = tn.TenantId 
                        WHERE t.HouseId = h.HouseId AND t.Status = 'Active' AND tn.IsArchived = 0) AS ActiveTenantCount,
                       -- Sum only monthly rent, NOT deposit amount
                       -- Only include tenancies for non-archived tenants
                       (SELECT ISNULL(SUM(t.RentAmountMonthly), 0) 
                        FROM Tenancy t 
                        INNER JOIN Tenant tn ON t.TenantId = tn.TenantId
                        WHERE t.HouseId = h.HouseId AND t.Status = 'Active' AND tn.IsArchived = 0) AS TotalMonthlyRent
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
