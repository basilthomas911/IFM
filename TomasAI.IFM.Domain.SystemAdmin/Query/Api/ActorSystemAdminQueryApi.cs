using TomasAI.IFM.Domain.SystemAdmin.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Query.Api;

/// <summary>Provides direct, in-process System Administration queries without actor messaging.</summary>
public sealed class ActorSystemAdminQueryApi : IActorSystemAdminQueryApi
{
    public Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync()
    {
        try
        {
            return Task.FromResult<ServiceResult<DatabaseNamesReadModel>>(
                new ServiceOk<DatabaseNamesReadModel>(SystemAdminQueryState.GetDatabaseNames()));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<DatabaseNamesReadModel>>(
                new ServiceFailed<DatabaseNamesReadModel>(GetDatabaseNamesQuery.ErrorId, ex.Message));
        }
    }
}
