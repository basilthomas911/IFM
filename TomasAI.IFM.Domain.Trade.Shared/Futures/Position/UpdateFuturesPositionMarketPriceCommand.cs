using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

[MessagePackObject]
public sealed record UpdateFuturesPositionMarketPriceCommand : FuturesPositionCommand
{
    public const string Verb = "UpdateFuturesPositionMarketPrice";
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public decimal Price { get; init; }
    [Key(6)] public long SourceSequence { get; init; }
    [Key(7)] public long RouteGeneration { get; init; }
    [Key(8)] public DateTime EffectiveAtUtc { get; init; }
}
