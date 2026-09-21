using System.Diagnostics;
using System.Text.Json;
using Npgsql;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>Low-rate test observer; overhead is intentionally shared by both compared variants.</summary>
internal sealed class EventLogSoakObserver : IAsyncDisposable
{
    sealed record Observation(double Seconds, long ManagedBytes, long WorkingSetBytes,
        long PrivateBytes, long QueueAndAdmissionDepth, long Commits,
        Dictionary<string, int> ActiveBackendWaits, JsonElement? ContainerStats);
    readonly CancellationTokenSource _stop = new();
    readonly List<Observation> _observations = [];
    readonly Task _run;
    readonly string _output;
    readonly Func<long> _depth;
    readonly Func<long> _commits;
    int _disposed;
    internal EventLogSoakObserver(string connection, string output, Func<long> depth, Func<long> commits)
    {
        _output = output;
        _depth = depth;
        _commits = commits;
        _run = Run(connection);
    }
    async Task Run(string connection)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            await using var db = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection)
                { Pooling = false, ApplicationName = "EventLogSoakObserver" }.ConnectionString);
            await db.OpenAsync(_stop.Token);
            using var process = Process.GetCurrentProcess();
            using var tick = new PeriodicTimer(TimeSpan.FromSeconds(1));
            var nextContainerSample = 0d;
            do
            {
                var waits = new Dictionary<string, int>();
                await using (var command = new NpgsqlCommand("""
                    SELECT coalesce(wait_event_type||':'||wait_event,'CPU-or-unreported'),count(*)::int
                    FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()
                    AND state='active' GROUP BY 1
                    """, db) { CommandTimeout = 3 })
                await using (var reader = await command.ExecuteReaderAsync(_stop.Token))
                    while (await reader.ReadAsync(_stop.Token))
                        waits.Add(reader.GetString(0), reader.GetInt32(1));
                JsonElement? container = null;
                if (timer.Elapsed.TotalSeconds >= nextContainerSample)
                {
                    container = await ContainerStats();
                    nextContainerSample = timer.Elapsed.TotalSeconds + 5;
                }
                process.Refresh();
                _observations.Add(new(timer.Elapsed.TotalSeconds, GC.GetTotalMemory(false),
                    process.WorkingSet64, process.PrivateMemorySize64, _depth(), _commits(), waits, container));
            } while (await tick.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    static async Task<JsonElement> ContainerStats()
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        // Pressure mode validates this exact labelled, exclusive-volume, loopback container before fixtures.
        foreach (var arg in new[] { "stats", "--no-stream", "--format", "{{json .}}", "ifm-eventlog-benchmark-20260919" })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start docker stats.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        if (process.ExitCode != 0) throw new InvalidOperationException("Docker stats failed: " + await stderr);
        using var document = JsonDocument.Parse(await stdout);
        return document.RootElement.Clone();
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        try
        {
            await _run.WaitAsync(TimeSpan.FromSeconds(15));
            if (_observations.Count == 0) throw new InvalidOperationException("Soak observer produced no observations.");
        }
        finally
        {
            await File.WriteAllTextAsync(_output, JsonSerializer.Serialize(_observations,
                new JsonSerializerOptions { WriteIndented = true }));
            _stop.Dispose();
        }
    }
}
