using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
public sealed class FuturesOptionTradeQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IBlackboardService blackboardService, ILogger<FuturesOptionTradeQueryActor> logger, TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.TradeRealtimeCacheRegistry? realtimeCache = null) : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, FuturesOptionTradeQueryActor.ActorName)), IFuturesOptionTradeQueryContext
{
    public TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.TradeRealtimeCacheRegistry? RealtimeCache { get; } = realtimeCache;
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public IBlackboardService BlackboardService { get; } = blackboardService;
    public ILogger<FuturesOptionTradeQueryActor> Logger { get; } = logger;
}
