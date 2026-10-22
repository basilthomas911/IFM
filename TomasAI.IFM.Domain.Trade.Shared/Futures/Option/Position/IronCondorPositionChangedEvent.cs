using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

[MessagePackObject]
public sealed record IronCondorPositionChangedEvent : PositionChangedEvent
{
    /// <summary>Identifies replaceable quote-driven marks; execution/fill transitions never set this flag.</summary>
    [Key(9)] public bool IsRealtimeMarketObservation { get; init; }
}
