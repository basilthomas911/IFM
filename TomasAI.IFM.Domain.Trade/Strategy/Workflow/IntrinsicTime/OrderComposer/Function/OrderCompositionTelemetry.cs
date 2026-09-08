using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Framework.Serialization;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;
/// <summary>Bounded operational measurements; identities and arbitrary provider reasons never become metric labels.</summary>
public static class OrderCompositionTelemetry
{
    static readonly Meter Meter = new("TomasAI.IFM.OrderComposition");
    static readonly Counter<long> Completed = Meter.CreateCounter<long>("order_composition.completed");
    static readonly Counter<long> Replayed = Meter.CreateCounter<long>("order_composition.replayed");
    static readonly Counter<long> Failed = Meter.CreateCounter<long>("order_composition.failed");
    static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("order_composition.duration", "ms");
    static readonly Histogram<int> Generated = Meter.CreateHistogram<int>("order_composition.generated");
    static readonly Histogram<int> Eligible = Meter.CreateHistogram<int>("order_composition.eligible");
    static readonly Histogram<int> Rejected = Meter.CreateHistogram<int>("order_composition.rejected");
    static readonly Histogram<long> Content = Meter.CreateHistogram<long>("order_composition.result_content", "bytes");
    public static void Replay() => Replayed.Add(1);
    public static void Record(OrderCompositionResult result, double elapsed)
    {
        KeyValuePair<string, object?>[] tags = [new("horizon", result.TargetHorizon.ToString()), new("outcome", result.Outcome.ToString())];
        Completed.Add(1, tags); Duration.Record(elapsed, tags); Generated.Record(result.CandidateCounts.Generated, tags);
        Eligible.Record(result.CandidateCounts.Eligible, tags); Rejected.Record(result.CandidateCounts.Rejected, tags);
        Content.Record(MessagePackBinarySerializer.MeasureContent(result), tags);
    }
    public static void Failure(string reason)
    {
        var category = reason.Split('.').ElementAtOrDefault(1);
        if (category is not ("CONTRACT" or "CONFIG" or "TIME" or "PROJECTION" or "PERSISTENCE" or "RESULT" or "TRANSPORT" or "PRICING" or "MARKET" or "CALCULATION")) category = "OTHER";
        Failed.Add(1, new KeyValuePair<string, object?>("category", category));
    }
}
