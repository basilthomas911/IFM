using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ServiceApi;

/// <summary>
/// Defines System Administration queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorSystemAdminQueryApi : ISystemAdminQueryApi
{
    Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync(
        CancellationToken cancellationToken);
}
