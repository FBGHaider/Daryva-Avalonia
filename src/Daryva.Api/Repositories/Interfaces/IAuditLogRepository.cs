using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IAuditLogRepository
{
    void Add(AuditLog entry);
}
