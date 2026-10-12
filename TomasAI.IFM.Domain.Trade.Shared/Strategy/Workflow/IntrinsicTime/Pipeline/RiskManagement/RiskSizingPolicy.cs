using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

/// <summary>Engineering defaults apply independently to exactly one triggering timeframe.</summary>
[MessagePackObject]
public sealed record RiskSizingPolicy([property: Key(0)] TimeFrameType Horizon,
    [property: Key(1)] int MaximumUnits = 10,
    [property: Key(2)] decimal PerTradeRiskFraction = .01m)
{
    public static RiskSizingPolicy Default(TimeFrameType horizon)
    {
        if (horizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly)) throw new ArgumentOutOfRangeException(nameof(horizon));
        return new(horizon);
    }
}
