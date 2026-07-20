using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.QueryParameters;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// Provides methods for querying system administration related data such as database names.
/// </summary>
/// <param name="querySvc">The query service API client.</param>
public class SystemAdminQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), ISystemAdminQueryApi
{
    /// <summary>
    /// Retrieves the available database names used by the system.
    /// </summary>
    /// <returns>A <see cref="DatabaseNamesReadModel"/> wrapped in a <see cref="ServiceResult"/>.</returns>
    public async Task<ServiceResult<DatabaseNamesReadModel>> GetDatabaseNamesAsync()
    {
        var entityId = new GetDatabaseNamesParameter();
        var query = new GetDatabaseNamesQuery()
        {
            Subject = new ActorSubject(ActorType.Query, GetDatabaseNamesQuery.Actor, GetDatabaseNamesQuery.Verb, entityId.Format())
        };
        return await RequestAsync<GetDatabaseNamesQuery, DatabaseNamesReadModel>(query.Subject, query);
    }
}
