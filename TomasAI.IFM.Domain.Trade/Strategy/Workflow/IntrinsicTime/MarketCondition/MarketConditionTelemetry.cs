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
    static readonly Counter<long> AssessmentAvailability = Meter.CreateCounter<long>("ifm.market_condition.assessment.availability");
    static readonly Counter<long> AssessmentSources = Meter.CreateCounter<long>("ifm.market_condition.assessment.source");
    static readonly Counter<long> Reasons = Meter.CreateCounter<long>("ifm.market_condition.reason");
    static readonly Counter<long> Timeouts = Meter.CreateCounter<long>("ifm.market_condition.timeout");
    static readonly Counter<long> Expired = Meter.CreateCounter<long>("ifm.market_condition.expired_before_acceptance");
    static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "ifm.market_condition.duration", "ms");
    static readonly Histogram<double> SourceAge = Meter.CreateHistogram<double>(
        "ifm.market_condition.source_age", "s");
    public static Activity? Start(string operation) => Activities.StartActivity(operation, ActivityKind.Internal);

    public static void RecordAssessment(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentResult result,double elapsedMilliseconds)
    {
        var horizon=Horizon(result.TargetHorizon);
        AssessmentAvailability.Add(1,new("horizon",horizon),new("availability",result.Assessment.Availability.ToString()));
        Duration.Record(elapsedMilliseconds,new("mode","assessment"),new("horizon",horizon));
        foreach(var source in result.Assessment.EvidenceItems.Where(x=>x.Feature=="SourceObservation"))
        {
            AssessmentSources.Add(1,new("horizon",horizon),new("source",source.SourceId),new("availability",source.Availability.ToString()));
            RecordSourceAge(source.SourceId,source.AgeSeconds,result.TargetHorizon);
        }
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
