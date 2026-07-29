using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ServiceApi
{
    public interface ISystemAdminQueryApi
    {
        Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync();
    }
}
