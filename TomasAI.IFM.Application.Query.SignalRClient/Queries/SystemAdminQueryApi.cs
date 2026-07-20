using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class SystemAdminQueryApi : ISystemAdminQueryApi
    {
        private IQueryService _querySvc;

        public SystemAdminQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
        }

        public async Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync()
            => await _querySvc.ExecuteQueryAsync<DatabaseNamesReadModel>(new GetDatbaseNamesQuery {});

    }
}
