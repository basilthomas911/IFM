using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
public static class TradeSelectionTelemetry
{
    static readonly Meter Meter=new("TomasAI.IFM.TradeSelection");
    static readonly Histogram<double> Duration=Meter.CreateHistogram<double>("trade_selection.duration","ms");
    static readonly Counter<long> Completions=Meter.CreateCounter<long>("trade_selection.completed");
    static readonly Counter<long> Failures=Meter.CreateCounter<long>("trade_selection.failed");
    static readonly Counter<long> Replays=Meter.CreateCounter<long>("trade_selection.replayed");
    static readonly Counter<long> Conflicts=Meter.CreateCounter<long>("trade_selection.conflicting_duplicate");
    static readonly Histogram<double> PendingAge=Meter.CreateHistogram<double>("trade_selection.reservation_pending_age","ms");
    public static void Replay()=>Replays.Add(1);
    public static void ReservationPending(DateTime since,DateTime now)=>PendingAge.Record(Math.Max(0,(now-since).TotalMilliseconds));
    static readonly Histogram<int> Candidates=Meter.CreateHistogram<int>("trade_selection.candidates");
    public static void Record(TradeSelectionResult result,double elapsed)
    {
        KeyValuePair<string,object?>[] tags=[new("horizon",result.DecisionHorizon.ToString()),new("outcome",result.Outcome.ToString())];
        Duration.Record(elapsed,tags);Completions.Add(1,tags);Candidates.Record(result.CandidateDecisions.Length,tags);
    }
    public static void Failure(string reason)
    {
        if(reason=="TS.CONTRACT.CONFLICTING_DUPLICATE")Conflicts.Add(1);
        var category=reason.Split('.').ElementAtOrDefault(1);
        if(category is not ("CONTRACT" or "CONFIG" or "TIME" or "PROJECTION" or "PERSISTENCE" or "UPSTREAM" or "RESULT" or "TRANSPORT"))category="OTHER";
        Failures.Add(1,new KeyValuePair<string,object?>("category",category));
    }
}
