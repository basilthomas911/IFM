using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.Actor;
/// <summary>Per-RecordId function context; no native resource references are permitted.</summary>
public sealed class MarketDataFeedRecoveryRecordFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new(ActorType.Function, MarketDataFeedRecoveryRecordFunctionActor.ActorName)),
      IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor>;
