using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Queries;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class ApplicationQueryApi(IActorProducer actorProducer)
    : NatsClientApi(actorProducer), IApplicationQueryApi
{
    public async Task<ServiceResult<ApplicationStartupStatus>> GetStartupStatusAsync()
    {
        var query = new GetApplicationStartupStatusQuery
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetApplicationStartupStatusQuery.Actor,
                GetApplicationStartupStatusQuery.Verb,
                "current")
        };
        return await RequestAsync<GetApplicationStartupStatusQuery, ApplicationStartupStatus>(
            query.Subject,
            query).ConfigureAwait(false);
    }
}
