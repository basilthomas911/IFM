using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// Provides methods for querying system administration related data such as database names.
/// </summary>
/// <param name="querySvc">The query service API client.</param>
public class SystemAdminQueryApi(IQueryServiceApi querySvc) : ISystemAdminQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Retrieves the available database names used by the system.
    /// </summary>
    /// <returns>A <see cref="DatabaseNamesReadModel"/> wrapped in a <see cref="ServiceResult"/>.</returns>
    public async Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync()
        => await _querySvc.ExecuteQueryAsync(SystemAdminQueryUriPath.GetDatabaseNames, new GetDatabaseNamesQuery());
}
