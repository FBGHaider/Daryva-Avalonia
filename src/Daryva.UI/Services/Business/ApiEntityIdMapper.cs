using System.Collections.Concurrent;
using System.Threading;

namespace Daryva.Services.Business;

public class ApiEntityIdMapper : IApiEntityIdMapper
{
    private readonly ConcurrentDictionary<Guid, int> _houseIds = new();
    private readonly ConcurrentDictionary<Guid, int> _tenantIds = new();
    private readonly ConcurrentDictionary<Guid, int> _tenancyIds = new();
    private readonly ConcurrentDictionary<int, Guid> _houseIdReverse = new();
    private readonly ConcurrentDictionary<int, Guid> _tenantIdReverse = new();
    private readonly ConcurrentDictionary<int, Guid> _tenancyIdReverse = new();

    private int _nextHouseId;
    private int _nextTenantId;
    private int _nextTenancyId;

    public int MapHouseId(Guid houseApiId)
    {
        var id = _houseIds.GetOrAdd(houseApiId, _ => Interlocked.Increment(ref _nextHouseId));
        _houseIdReverse[id] = houseApiId;
        return id;
    }

    public int MapTenantId(Guid tenantApiId)
    {
        var id = _tenantIds.GetOrAdd(tenantApiId, _ => Interlocked.Increment(ref _nextTenantId));
        _tenantIdReverse[id] = tenantApiId;
        return id;
    }

    public int MapTenancyId(Guid tenancyApiId)
    {
        var id = _tenancyIds.GetOrAdd(tenancyApiId, _ => Interlocked.Increment(ref _nextTenancyId));
        _tenancyIdReverse[id] = tenancyApiId;
        return id;
    }

    public Guid? TryGetHouseApiId(int localHouseId) => _houseIdReverse.TryGetValue(localHouseId, out var g) ? g : null;
    public Guid? TryGetTenantApiId(int localTenantId) => _tenantIdReverse.TryGetValue(localTenantId, out var g) ? g : null;
    public Guid? TryGetTenancyApiId(int localTenancyId) => _tenancyIdReverse.TryGetValue(localTenancyId, out var g) ? g : null;
}
