using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

[MessagePackObject]
public sealed record StrategyTradePlanSnapshot
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public long PlanRevision { get; init; }
    [Key(4)] public decimal OpeningValue { get; init; }
    [Key(5)] public decimal CurrentValue { get; init; }
    [Key(6)] public decimal TotalPnl { get; init; }
    [Key(7)] public decimal ForwardTradePrice { get; init; }
    [Key(8)] public decimal ForwardPnl { get; init; }
    [Key(9)] public decimal ForwardLoss { get; init; }
    [Key(10)] public TradePlanState State { get; init; }
    [Key(11)] public TradePlanAction Action { get; init; }
    [Key(12)] public bool RequiresExit { get; init; }
    [Key(13)] public bool MaterialChange { get; init; }
    [Key(14)] public string ReasonCode { get; init; } = string.Empty;
    [Key(15)] public string Explanation { get; init; } = string.Empty;
    [Key(16)] public TradePlanParameters Parameters { get; init; } = new();
    [Key(17)] public string ContentHash { get; init; } = string.Empty;
    [Key(18)] public DateTime CalculatedAtUtc { get; init; }
    /// <summary>Gets strategy-specific legacy monitoring values while retaining old generic snapshots.</summary>
    [Key(19)] public IronCondorTradePlanSnapshot? IronCondorTradePlanSnapshot { get; init; }
}
