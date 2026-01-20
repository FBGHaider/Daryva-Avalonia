using FBGRentora.MVVM.Models;

namespace FBGRentora.Services.Data
{
    public interface IRentChargeRepository
    {
        Task<RentCharge?> GetRentChargeAsync(int tenancyId, int periodYear, int periodMonth);
        Task<RentCharge?> GetRentChargeByIdAsync(int rentChargeId);
        Task<int> CreateRentChargeAsync(RentCharge charge);
        Task UpdateRentChargeStatusAsync(int rentChargeId, string status);
        Task<IEnumerable<RentCharge>> GetRentChargesByTenancyIdAsync(int tenancyId);
    }
}
