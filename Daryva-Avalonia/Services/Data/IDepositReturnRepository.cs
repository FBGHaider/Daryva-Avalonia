using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface IDepositReturnRepository
    {
        Task<int> CreateAsync(DepositReturn depositReturn);
        Task<DepositReturn?> GetByTenancyIdAsync(int tenancyId);
        Task<IReadOnlyDictionary<int, DepositReturn>> GetByTenancyIdsAsync(IEnumerable<int> tenancyIds);
    }
}
