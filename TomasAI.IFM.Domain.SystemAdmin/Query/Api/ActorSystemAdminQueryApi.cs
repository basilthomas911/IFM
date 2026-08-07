using TomasAI.IFM.Domain.SystemAdmin.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Query.Api;

/// <summary>
/// Provides direct, in-process System Administration queries without actor messaging.
/// </summary>
/// <remarks>
/// Database-name information is read from <see cref="SystemAdminQueryState"/> and returned through the same
/// typed service-result contract used by storage-backed actor query APIs. The implementation has no actor
/// context or mutable instance state and may be registered as a singleton.
/// </remarks>
public sealed class ActorSystemAdminQueryApi : IActorSystemAdminQueryApi
{
    static readonly Task<ServiceResult<DatabaseNamesReadModel>> DatabaseNamesResult =
        Task.FromResult<ServiceResult<DatabaseNamesReadModel>>(
            new ServiceOk<DatabaseNamesReadModel>(SystemAdminQueryState.GetDatabaseNames()));

    /// <summary>
    /// Gets database names.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync() => DatabaseNamesResult;

    public Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync(
        CancellationToken cancellationToken)
        => cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<ServiceResult<DatabaseNamesReadModel>>(cancellationToken)
            : DatabaseNamesResult;
}
