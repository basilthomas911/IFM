using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.Actor;
/// <summary>Normal command context: records copied facts and never controls datasets.</summary>
public sealed class MarketDataFeedRecoveryCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new(ActorType.Command, MarketDataFeedRecoveryCommandActor.ActorName)),
      ICommandActorContext<MarketDataFeedRecoveryCommandActor>;
