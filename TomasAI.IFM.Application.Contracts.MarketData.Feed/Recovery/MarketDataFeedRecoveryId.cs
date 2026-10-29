using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Operation identity for aggregates; record identity for the acknowledgement function.</summary>
[MessagePackObject]
public sealed record MarketDataFeedRecoveryId([property: Key(0)] Guid Value) : IActorEntityId
{
    public string Format() => Value.ToString("D");
}
