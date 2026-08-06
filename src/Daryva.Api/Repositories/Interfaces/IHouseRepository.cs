using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IHouseRepository
{
    Task<List<House>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<House?> GetByIdAsync(Guid houseId, CancellationToken cancellationToken = default);

    /// <summary>Tracked -- for in-place mutation (update/archive/delete).</summary>
    Task<House?> GetTrackedByIdAsync(Guid houseId, CancellationToken cancellationToken = default);

    void Add(House house);

    void Remove(House house);
}
