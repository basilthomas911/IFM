using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Immutable native RecoveryFailed fact.</summary>
[MessagePackObject]
public sealed record MarketDataFeedRecoveryFailedEvent : MarketDataFeedRecoveryFact;
