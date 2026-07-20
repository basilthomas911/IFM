using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client;

public class SystemAdminQueryApi(IQueryService querySvc) : ISystemAdminQueryApi
{
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);
    readonly string _controller = "SystemAdmin";

    public async Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync()
        => await _querySvc.ExecuteApiQueryAsync(new GetDatabaseNamesQuery(), _controller);

}
