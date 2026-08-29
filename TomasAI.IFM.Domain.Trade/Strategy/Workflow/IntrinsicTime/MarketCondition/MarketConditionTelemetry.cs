using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;

/// <summary>Bounded-cardinality telemetry for the Market Condition production path.</summary>
public static class MarketConditionTelemetry
{
    public const string InstrumentationName = "TomasAI.IFM.Domain.Trade.MarketCondition";
    public static readonly ActivitySource Activities = new(InstrumentationName);
    public static readonly Meter Meter = new(InstrumentationName);

    static readonly Counter<long> Processing = Meter.CreateCounter<long>("ifm.market_condition.processing");
    static readonly Counter<long> Tradeability = Meter.CreateCounter<long>("ifm.market_condition.tradeability");
    static readonly Counter<long> Reasons = Meter.CreateCounter<long>("ifm.market_condition.reason");
    static readonly Counter<long> Timeouts = Meter.CreateCounter<long>("ifm.market_condition.timeout");
    static readonly Counter<long> Expired = Meter.CreateCounter<long>("ifm.market_condition.expired_before_acceptance");
    static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "ifm.market_condition.duration", "ms");
    static readonly Histogram<double> SourceAge = Meter.CreateHistogram<double>(
        "ifm.market_condition.source_age", "s");
    static readonly Histogram<double> Strength = Meter.CreateHistogram<double>(
        "ifm.market_condition.strength");
    static readonly Histogram<double> Confidence = Meter.CreateHistogram<double>(
        "ifm.market_condition.confidence");

    public static Activity? Start(string operation) => Activities.StartActivity(operation, ActivityKind.Internal);

    public static void RecordResult(MarketConditionResult result, double durationMilliseconds)
    {
        var horizon = Horizon(result.TargetHorizon);
        Processing.Add(1, new("outcome", "completed"), new("horizon", horizon));
        Tradeability.Add(1, new("outcome", result.Tradeability.ToString()), new("horizon", horizon));
        Duration.Record(durationMilliseconds, new("outcome", "completed"), new("horizon", horizon));
        Strength.Record((double)result.Strength,
            new KeyValuePair<string, object?>("horizon", horizon));
        Confidence.Record((double)result.Confidence,
            new KeyValuePair<string, object?>("horizon", horizon));
        foreach (var reason in result.BlockingReasons.Select(x => x.ReasonCode).Distinct(StringComparer.Ordinal))
            Reasons.Add(1, new("kind", "blocker"), new("code", reason), new("horizon", horizon));
    }

    public static void RecordFailure(MarketConditionFailureCategory category, string reason,
        TimeFrameType horizon, double durationMilliseconds)
    {
        var horizonName = Horizon(horizon);
        Processing.Add(1, new("outcome", "failed"), new("horizon", horizonName));
        Duration.Record(durationMilliseconds, new("outcome", "failed"), new("horizon", horizonName));
        Reasons.Add(1, new("kind", "failure"), new("code", reason), new("horizon", horizonName));
        if (category == MarketConditionFailureCategory.Timeout)
            Timeouts.Add(1, new KeyValuePair<string, object?>("horizon", horizonName));
    }

    public static void RecordSourceAge(string sourceCategory, decimal ageSeconds, TimeFrameType horizon) =>
        SourceAge.Record((double)ageSeconds, new("source", sourceCategory), new("horizon", Horizon(horizon)));

    public static void RecordExpired(TimeFrameType horizon) => Expired.Add(1,
        new KeyValuePair<string, object?>("horizon", Horizon(horizon)));

    static string Horizon(TimeFrameType value) => value switch
    {
        TimeFrameType.Daily => "daily",
        TimeFrameType.Weekly => "weekly",
        TimeFrameType.Monthly => "monthly",
        _ => "unknown"
    };
}
