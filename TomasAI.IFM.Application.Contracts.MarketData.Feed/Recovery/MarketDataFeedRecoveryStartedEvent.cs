using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Immutable native RecoveryStarted fact.</summary>
[MessagePackObject]
public sealed record MarketDataFeedRecoveryStartedEvent : MarketDataFeedRecoveryFact;
