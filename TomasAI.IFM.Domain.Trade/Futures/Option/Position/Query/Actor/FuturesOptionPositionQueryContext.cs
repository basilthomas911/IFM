using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
public sealed class FuturesOptionPositionQueryContext(IActorSupervisor a, IDbContextFactory d, ILogger<FuturesOptionPositionQueryActor> l) : QueryActorContext(a, new(ActorType.Query, FuturesOptionPositionQueryActor.ActorName)), IFuturesOptionPositionQueryContext
{
    public IDbContextFactory DbFactory { get; } = d;
    public ILogger<FuturesOptionPositionQueryActor> Logger { get; } = l;
}
