using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Query.Actor;
public sealed class FuturesTradeQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<FuturesTradeQueryActor> logger) : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, FuturesTradeQueryActor.ActorName)), IFuturesTradeQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesTradeQueryActor> Logger { get; } = logger;
}
