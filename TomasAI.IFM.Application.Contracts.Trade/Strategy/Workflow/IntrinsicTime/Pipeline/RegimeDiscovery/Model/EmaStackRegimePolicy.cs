using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the explicit EMA-only policy without reinterpreting historical composite results.</summary>
public static class EmaStackRegimePolicy
{
    public const string Version = "EmaStack.v1";
    public const string ReasonCode = "RD.POLICY.EMA_STACK_V1";
    public static TimeFrameType Frame(TimeFrameType horizon) => horizon switch
    {
        TimeFrameType.Daily => TimeFrameType.FiveMinutes,
        TimeFrameType.Weekly => TimeFrameType.FourHours,
        TimeFrameType.Monthly => TimeFrameType.Daily,
        _ => throw new ArgumentOutOfRangeException(nameof(horizon))
    };
    public static bool Applies(RegimeDiscoveryResult result) => result.Reasons.Any(reason => reason.Code == ReasonCode);
}
