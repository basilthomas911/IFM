using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Durable per-assignment arming evidence, subsequently carried into the filled position.</summary>
[MessagePackObject]
public sealed record DailyFuturesEntryState
{
    public const string Version = "DailyFuturesAtr14.v1";
    [Key(0)] public string Policy { get; init; } = Version;
    [Key(1)] public Guid DcTriggerId { get; init; }
    [Key(2)] public string ContractId { get; init; } = "";
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public DateTime DcAtUtc { get; init; }
    [Key(5)] public decimal DcPrice { get; init; }
    [Key(6)] public decimal Threshold { get; init; }
    [Key(7)] public DailyItiAtrThreshold? Atr { get; init; }
    [Key(8)] public bool Consumed { get; init; }
}
