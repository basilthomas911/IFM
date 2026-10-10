using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace TomasAI.IFM.Framework.Telemetry.Metrics;

/// <summary>Identifies one process lifetime independently of PID reuse and application restarts.</summary>
public static class ProcessGcRunIdentity
{
    /// <summary>Gets the operating-system process start time in UTC.</summary>
    public static DateTimeOffset StartedUtc { get; } = ReadStartTime();
    /// <summary>Gets the unique identity shared by GC history and OTel resource attributes.</summary>
    public static string InstanceId { get; } = $"{Environment.MachineName}/{Environment.ProcessId}/{StartedUtc.UtcTicks}";
    static DateTimeOffset ReadStartTime()
    {
        using var process = Process.GetCurrentProcess();
        return new DateTimeOffset(process.StartTime.ToUniversalTime());
    }
}

/// <summary>A timestamped process-lifetime GC observation; heap sizes describe the last GC, including fragmentation.</summary>
public sealed record ProcessGcSnapshot(
    string Service, string ProcessRunId, int ProcessId, DateTimeOffset ProcessStartedUtc,
    DateTimeOffset RecordedUtc, string Phase, double UptimeSeconds,
    int Gen0CollectionsSinceStart, int Gen1CollectionsSinceStart, int Gen2CollectionsSinceStart,
    long AllocatedBytesSinceStartApproximate, double PauseMillisecondsSinceStart,
    long LastGcHeapBytes, long LastGcCommittedBytes, long LastGcFragmentedBytes,
    long LastGcLargeObjectHeapBytes, long WorkingSetBytes);

/// <summary>Records cumulative runtime GC statistics from startup to normal shutdown without forcing a collection.</summary>
public sealed class ProcessGcStatisticsRecorder : IDisposable, IAsyncDisposable
{
    /// <summary>The meter exported through the existing IFM OTel pipeline.</summary>
    public const string MeterName = "TomasAI.IFM.ProcessGc";
    readonly string _service;
    readonly TimeSpan _interval;
    readonly Process _process = Process.GetCurrentProcess();
    readonly CancellationTokenSource _stop = new();
    readonly Meter _meter = new(MeterName);
    readonly Task _loop;
    ProcessGcSnapshot _latest;
    DateTimeOffset _lastWarning;
    int _disposed;

    /// <summary>Gets the durable JSON Lines history file for this process run.</summary>
    public string HistoryPath { get; }

    /// <summary>Starts lifetime recording, enabled by default; storage errors are reported without stopping trading.</summary>
    public static ProcessGcStatisticsRecorder? Start(IConfiguration configuration, string service)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        var options = configuration.GetSection("Telemetry:GcHistory");
        if (!options.GetValue("Enabled", true)) return null;
        var seconds = options.GetValue("SampleIntervalSeconds", 5);
        var days = options.GetValue("RetentionDays", 30);
        if (seconds < 1 || seconds > 300 || days < 1)
            throw new InvalidOperationException("GC history requires a 1?300 second sample interval and positive retention days.");
        var root = options.GetValue<string>("OutputDirectory");
        if (string.IsNullOrWhiteSpace(root))
        {
            var repository = Environment.GetEnvironmentVariable("IFM_REPOSITORY_ROOT");
            root = string.IsNullOrWhiteSpace(repository)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IFM", "telemetry", "gc")
                : Path.Combine(repository, ".artifacts", "telemetry", "gc");
        }
        return new(service, Path.GetFullPath(root), TimeSpan.FromSeconds(seconds), days);
    }

    ProcessGcStatisticsRecorder(string service, string directory, TimeSpan interval, int retentionDays)
    {
        _service = service; _interval = interval;
        var safeService = new string(service.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_').ToArray());
        HistoryPath = Path.Combine(directory, $"process-gc-{safeService}-{ProcessGcRunIdentity.StartedUtc:yyyyMMddTHHmmssfffffffZ}-{Environment.ProcessId}.jsonl");
        _latest = Capture("Started");
        _meter.CreateObservableCounter("ifm.process.gc.collections", CollectionCounts, "{collection}", "Collections since process start, by generation.");
        _meter.CreateObservableCounter("ifm.process.gc.allocated.bytes", () => GC.GetTotalAllocatedBytes(false), "By", "Approximate allocated bytes since process start.");
        _meter.CreateObservableCounter("ifm.process.gc.pause.seconds", () => GC.GetTotalPauseDuration().TotalSeconds, "s", "Total GC pause duration since process start.");
        _meter.CreateObservableGauge("ifm.process.uptime.seconds", () => (DateTimeOffset.UtcNow - ProcessGcRunIdentity.StartedUtc).TotalSeconds, "s");
        _meter.CreateObservableGauge("ifm.process.gc.last_collection.heap.bytes", () => Volatile.Read(ref _latest).LastGcHeapBytes, "By");
        _meter.CreateObservableGauge("ifm.process.gc.last_collection.fragmented.bytes", () => Volatile.Read(ref _latest).LastGcFragmentedBytes, "By");
        _meter.CreateObservableGauge("ifm.process.working_set.bytes", () => Volatile.Read(ref _latest).WorkingSetBytes, "By");
        _loop = Task.Run(() => RecordAsync(directory, retentionDays));
    }

    IEnumerable<Measurement<long>> CollectionCounts()
    {
        for (var generation = 0; generation <= 2; generation++)
            yield return new(GC.CollectionCount(generation), new KeyValuePair<string, object?>("gc.heap.generation", $"gen{generation}"));
    }

    /// <summary>Reads counters accumulated by the CLR since process start, rather than resetting them at each sample.</summary>
    public ProcessGcSnapshot Capture(string phase = "Sample")
    {
        var now = DateTimeOffset.UtcNow;
        var memory = GC.GetGCMemoryInfo();
        _process.Refresh();
        return new(_service, ProcessGcRunIdentity.InstanceId, Environment.ProcessId, ProcessGcRunIdentity.StartedUtc,
            now, phase, (now - ProcessGcRunIdentity.StartedUtc).TotalSeconds,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            GC.GetTotalAllocatedBytes(false), GC.GetTotalPauseDuration().TotalMilliseconds,
            memory.HeapSizeBytes, memory.TotalCommittedBytes, memory.FragmentedBytes,
            memory.GenerationInfo.Length > 3 ? memory.GenerationInfo[3].SizeAfterBytes : 0, _process.WorkingSet64);
    }

    async Task RecordAsync(string directory, int retentionDays)
    {
        try
        {
            try
            {
                Directory.CreateDirectory(directory);
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                foreach (var file in Directory.EnumerateFiles(directory, "process-gc-*.jsonl"))
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch (Exception exception) { Warn(exception); }
            await WriteAsync("Started", _stop.Token);
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(_stop.Token)) await WriteAsync("Sample", _stop.Token);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception exception) { Warn(exception); }
        finally
        {
            using var finalWrite = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await WriteAsync("Stopped", finalWrite.Token);
        }
    }

    async Task WriteAsync(string phase, CancellationToken token)
    {
        try
        {
            var snapshot = Capture(phase);
            Volatile.Write(ref _latest, snapshot);
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            await File.AppendAllTextAsync(HistoryPath, JsonSerializer.Serialize(snapshot) + "\n", token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { Warn(exception); }
    }

    void Warn(Exception exception)
    {
        if (DateTimeOffset.UtcNow - _lastWarning < TimeSpan.FromMinutes(1)) return;
        _lastWarning = DateTimeOffset.UtcNow;
        Log.Warning(exception, "{Component}.{Method} GC history write failed; Service={Service}; HistoryPath={HistoryPath}",
            nameof(ProcessGcStatisticsRecorder), nameof(WriteAsync), _service, HistoryPath);
    }

    /// <summary>Stops sampling and saves a bounded final lifetime observation on normal exit.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        if (await Task.WhenAny(_loop, Task.Delay(TimeSpan.FromSeconds(3))) == _loop)
        {
            await _loop;
            _process.Dispose(); _stop.Dispose();
        }
        else Log.Warning("{Component}.{Method} GC history shutdown timed out; HistoryPath={HistoryPath}", nameof(ProcessGcStatisticsRecorder), nameof(DisposeAsync), HistoryPath);
        _meter.Dispose();
    }

    /// <summary>Saves the final observation during synchronous application shutdown.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
