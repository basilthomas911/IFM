using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Commission evidence retained until its external execution arrives.</summary>
[MessagePackObject]
public sealed record PendingExecutionCostEvidence
{
    [Key(0)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(1)] public decimal Commission { get; init; }
}
