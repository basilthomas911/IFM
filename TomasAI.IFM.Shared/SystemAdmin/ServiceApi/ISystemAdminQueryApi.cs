using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Shared.SystemAdmin.ServiceApi
{
    public interface ISystemAdminQueryApi
    {
        Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync();
    }
}
