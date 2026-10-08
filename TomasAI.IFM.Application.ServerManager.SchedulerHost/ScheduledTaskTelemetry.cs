using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;
/// <summary>Records scheduler boundary latency using bounded task/environment dimensions.</summary>
internal static class ScheduledTaskTelemetry
{
    private static readonly Meter Meter = new("TomasAI.IFM.ScheduledTasks");
    private static readonly Histogram<double> ApplyDuration = Meter.CreateHistogram<double>("ifm.scheduler.apply.duration", "ms");
    private static readonly Histogram<double> RunDuration = Meter.CreateHistogram<double>("ifm.scheduler.run.duration", "ms");
    /// <summary>Measures one reconciliation without schedule/run identities as metric labels.</summary>
    public static void Applied(string taskKey, string environment, long started) => ApplyDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, new("task", taskKey), new("environment", environment));
    /// <summary>Measures admitted work and receipt completion, including uncertain outcomes.</summary>
    public static void Ran(string taskKey, string environment, long started) => RunDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, new("task", taskKey), new("environment", environment));
}
