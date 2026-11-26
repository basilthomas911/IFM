using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.Text.Json;

if (args.Length is < 2 or > 4)
    throw new ArgumentException("Usage: TraceMetrics <input.nettrace> <output.json> [process-id] [mailbox-workload-scope]");
var input = Path.GetFullPath(args[0]);
var processFilter = args.Length >= 3 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : (int?)null;
var processes = new Dictionary<int, Metrics>();
var scope = args.Length == 4 ? args[3] : null;
var active = new HashSet<int>();
var workloadMarkers = new List<object>();
Metrics? Get(int id)
{
    if (scope is not null && !active.Contains(id)) return null;
    if (processFilter is { } selected && id != selected) return null;
    if (!processes.TryGetValue(id, out var value)) processes[id] = value = new();
    return value;
}
using (var source = new EventPipeEventSource(input))
{
    source.Dynamic.All += data =>
    {
        if (data.ProviderName == "IFM-Mailbox-Qualification")
            workloadMarkers.Add(new { data.ProcessID, data.EventName, data.TimeStampRelativeMSec,
                Payload = data.PayloadNames.ToDictionary(name => name, name => Convert.ToString(data.PayloadByName(name), System.Globalization.CultureInfo.InvariantCulture)) });
        if (scope is null || data.ProviderName != "IFM-Mailbox-Qualification") return;
        if (data.EventName is "WorkloadStart" or "Workload/Start" && Convert.ToString(data.PayloadByName("name"), System.Globalization.CultureInfo.InvariantCulture) == scope)
        { active.Add(data.ProcessID); _ = Get(data.ProcessID); }
        if (data.EventName is "WorkloadStop" or "Workload/Stop" && Convert.ToString(data.PayloadByName("name"), System.Globalization.CultureInfo.InvariantCulture) == scope)
            active.Remove(data.ProcessID);
    };
    source.Clr.GCStart += data =>
    {
        if (Get(data.ProcessID) is { } value && data.Depth is >= 0 and <= 2) value.Collections[data.Depth]++;
    };
    source.Clr.GCSuspendEEStart += data =>
    {
        if (Get(data.ProcessID) is not { } value) return;
        var reason = data.Reason.ToString();
        value.SuspensionReasons[reason] = value.SuspensionReasons.GetValueOrDefault(reason) + 1;
        if (data.Reason is GCSuspendEEReason.SuspendForGC or GCSuspendEEReason.SuspendForGCPrep)
            value.SuspendedAt ??= data.TimeStampRelativeMSec;
    };
    source.Clr.GCRestartEEStop += data =>
    {
        if (Get(data.ProcessID) is { SuspendedAt: { } started } value)
        { value.PauseMilliseconds.Add(Math.Max(0, data.TimeStampRelativeMSec - started)); value.SuspendedAt = null; }
    };
    source.Clr.GCHeapStats += data =>
    {
        if (Get(data.ProcessID) is not { } value) return;
        value.PeakReportedHeapBytes = Math.Max(value.PeakReportedHeapBytes, data.GenerationSize0 + data.GenerationSize1 + data.GenerationSize2 + data.GenerationSize3 + data.GenerationSize4);
        value.PeakReportedLohBytes = Math.Max(value.PeakReportedLohBytes, data.GenerationSize3);
        value.PeakReportedPohBytes = Math.Max(value.PeakReportedPohBytes, data.GenerationSize4);
    };
    source.Clr.GCAllocationTick += data =>
    {
        if (Get(data.ProcessID) is not { } value) return;
        value.AllocationTickEvents++;
        var amount = data.AllocationAmount64 != 0 ? data.AllocationAmount64 : data.AllocationAmount;
        value.SampledAllocationTickBytes += amount;
        if (data.AllocationKind == GCAllocationKind.Large)
        { value.SampledLargeAllocationTickBytes += amount; value.LargeAllocationTickEvents++; }
    };
    source.Clr.ContentionStop += data =>
    {
        if (Get(data.ProcessID) is not { } value) return;
        value.CompletedMonitorContentions++;
        var duration = data.DurationNs / 1_000_000;
        value.MonitorContentionMilliseconds += duration;
        value.MaximumMonitorContentionMilliseconds = Math.Max(value.MaximumMonitorContentionMilliseconds, duration);
    };
    source.Process();
}
string hash;
await using (var stream = File.OpenRead(input)) hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));
var result = new
{
    Trace = input, WorkloadScope = scope, WorkloadMarkers = workloadMarkers,
    Sha256 = hash,
    Note = "GC starts are exclusive counts by depth. Only SuspendForGC/GCPrep intervals count as GC pauses; other runtime suspensions are reported separately. Heap peaks are reported GC samples, not continuous process peaks. AllocationTick bytes are sampled evidence. Monitor contentions exclude async semaphore waits. Incomplete pauses at trace end are excluded.",
    Processes = processes.Select(pair => new
    {
        ProcessId = pair.Key, Gen0Starts = pair.Value.Collections[0], Gen1Starts = pair.Value.Collections[1], Gen2Starts = pair.Value.Collections[2],
        GcPauseCount = pair.Value.PauseMilliseconds.Count,
        GcPauseTotalMilliseconds = pair.Value.PauseMilliseconds.Sum(),
        GcPauseMaximumMilliseconds = pair.Value.PauseMilliseconds.DefaultIfEmpty().Max(),
        pair.Value.PeakReportedHeapBytes, pair.Value.PeakReportedLohBytes, pair.Value.PeakReportedPohBytes,
        pair.Value.SampledAllocationTickBytes, pair.Value.AllocationTickEvents, pair.Value.SampledLargeAllocationTickBytes, pair.Value.LargeAllocationTickEvents, pair.Value.SuspensionReasons, pair.Value.CompletedMonitorContentions,
        pair.Value.MonitorContentionMilliseconds, pair.Value.MaximumMonitorContentionMilliseconds,
        IncompletePause = pair.Value.SuspendedAt is not null
    })
};
await File.WriteAllTextAsync(Path.GetFullPath(args[1]), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Analyzed {processes.Count} process(es): {args[1]}");

sealed class Metrics
{
    public long[] Collections { get; } = new long[3];
    public List<double> PauseMilliseconds { get; } = [];
    public Dictionary<string, long> SuspensionReasons { get; } = [];
    public double? SuspendedAt;
    public long PeakReportedHeapBytes, PeakReportedLohBytes, PeakReportedPohBytes;
    public long SampledAllocationTickBytes, AllocationTickEvents, SampledLargeAllocationTickBytes, LargeAllocationTickEvents, CompletedMonitorContentions;
    public double MonitorContentionMilliseconds, MaximumMonitorContentionMilliseconds;
}
