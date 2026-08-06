namespace Daryva.Services.Business;

public interface IApiEntityIdMapper
{
    int MapHouseId(Guid houseApiId);
    int MapTenantId(Guid tenantApiId);
    int MapTenancyId(Guid tenancyApiId);
    Guid? TryGetHouseApiId(int localHouseId);
    Guid? TryGetTenantApiId(int localTenantId);
    Guid? TryGetTenancyApiId(int localTenancyId);
}
