using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
public sealed class FuturesPositionQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<FuturesPositionQueryActor> logger) : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, FuturesPositionQueryActor.ActorName)), IFuturesPositionQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesPositionQueryActor> Logger { get; } = logger;
}
