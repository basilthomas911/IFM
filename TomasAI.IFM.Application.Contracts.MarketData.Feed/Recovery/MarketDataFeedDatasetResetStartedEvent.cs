using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Immutable native DatasetResetStarted fact.</summary>
[MessagePackObject]
public sealed record MarketDataFeedDatasetResetStartedEvent : MarketDataFeedRecoveryFact;
