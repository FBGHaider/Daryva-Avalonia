using System.Collections.Concurrent;
using System.Threading;

namespace Daryva.Services.Business;

public interface IApiEntityIdMapper
{
    int MapHouseId(Guid houseApiId);
    int MapTenantId(Guid tenantApiId);
    int MapTenancyId(Guid tenancyApiId);
}

public class ApiEntityIdMapper : IApiEntityIdMapper
{
    private readonly ConcurrentDictionary<Guid, int> _houseIds = new();
    private readonly ConcurrentDictionary<Guid, int> _tenantIds = new();
    private readonly ConcurrentDictionary<Guid, int> _tenancyIds = new();

    private int _nextHouseId;
    private int _nextTenantId;
    private int _nextTenancyId;

    public int MapHouseId(Guid houseApiId)
        => _houseIds.GetOrAdd(houseApiId, _ => Interlocked.Increment(ref _nextHouseId));

    public int MapTenantId(Guid tenantApiId)
        => _tenantIds.GetOrAdd(tenantApiId, _ => Interlocked.Increment(ref _nextTenantId));

    public int MapTenancyId(Guid tenancyApiId)
        => _tenancyIds.GetOrAdd(tenancyApiId, _ => Interlocked.Increment(ref _nextTenancyId));
}
