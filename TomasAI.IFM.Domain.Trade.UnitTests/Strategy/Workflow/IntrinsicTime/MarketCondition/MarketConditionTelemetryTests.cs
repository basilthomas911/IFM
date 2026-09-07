using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionTelemetryTests
{
    [Fact]
    public void Assessment_emits_bounded_metric_dimensions()
    {
        var activities = new ConcurrentQueue<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MarketConditionTelemetry.InstrumentationName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Enqueue
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new ConcurrentQueue<(string Name, string[] Tags)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MarketConditionTelemetry.InstrumentationName)
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Enqueue((instrument.Name, tags.ToArray().Select(x => x.Key).ToArray())));
        meterListener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Enqueue((instrument.Name, tags.ToArray().Select(x => x.Key).ToArray())));
        meterListener.Start();

        var command = AssessmentFixture.Command();
        var result = MarketConditionAssessmentCalculationTests.Calculate(command,
            MarketConditionAssessmentCalculationTests.Snapshot(command));
        MarketConditionTelemetry.RecordAssessment(result, 1d);
        MarketConditionTelemetry.RecordFailure(MarketConditionFailureCategory.Timeout,
            "MC_TIMEOUT", TimeFrameType.Daily, 5d);
        MarketConditionTelemetry.RecordSourceAge("futures", 1.25m, TimeFrameType.Daily);
        MarketConditionTelemetry.RecordExpired(TimeFrameType.Daily);

        meterListener.Dispose();
        activityListener.Dispose();
        var capturedMeasurements = measurements.ToArray();

        capturedMeasurements.Select(x => x.Name).Should().Contain([
            "ifm.market_condition.processing", "ifm.market_condition.assessment.availability",
            "ifm.market_condition.duration", "ifm.market_condition.reason",
            "ifm.market_condition.timeout", "ifm.market_condition.source_age",
            "ifm.market_condition.expired_before_acceptance"]);
        var allowedTags = new HashSet<string>(["outcome", "horizon", "kind", "code", "source", "availability", "mode"],
            StringComparer.Ordinal);
        capturedMeasurements.SelectMany(x => x.Tags).Should().OnlyContain(tag => allowedTags.Contains(tag));
        capturedMeasurements.SelectMany(x => x.Tags).Should().NotContain(tag => tag.Contains("id", StringComparison.OrdinalIgnoreCase));
    }
}
