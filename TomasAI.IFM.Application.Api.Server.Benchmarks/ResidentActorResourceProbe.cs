using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using TomasAI.IFM.TestSupport.ResidentActors;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Separate resource and burst/idle qualification; never runs concurrently with timed BDN cases.</summary>
public static class ResidentActorResourceProbe
{
    public static async Task RunAsync(string output, int soakSeconds)
    {
        long databaseActivities = 0, statementActivities = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                Interlocked.Increment(ref databaseActivities);
                if (activity.GetTagItem("db.query.text") is not null || activity.GetTagItem("db.statement") is not null)
                    Interlocked.Increment(ref statementActivities);
            }
        };
        ActivitySource.AddActivityListener(listener);
        await using var database = new ResidentPilotDatabase();
        await database.InitializeAsync();
        var rows = new List<object>();
        var nextKey = 10000;
        using var process = Process.GetCurrentProcess();
        foreach (var kind in Enum.GetValues<ResidentPilotKind>())
        for (var repetition = 0; repetition < 3; repetition++)
        foreach (var enabled in repetition % 2 == 0 ? new[] { false, true } : new[] { true, false })
        {
            await using var session = await database.CreateSessionAsync(kind, enabled, 64);
            var keys = Enumerable.Range(nextKey, 16).ToArray(); nextKey += 16;
            foreach (var key in keys) Check(await session.RunAsync(session.Request(key, 1)));
            database.ClearCallHistory();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            process.Refresh();
            var dbStart = Volatile.Read(ref databaseActivities); var sqlStart = Volatile.Read(ref statementActivities);
            var cpu = process.TotalProcessorTime; var allocated = GC.GetTotalAllocatedBytes(true);
            var pause = GC.GetTotalPauseDuration(); var contention = Monitor.LockContentionCount;
            var gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var samples = new ConcurrentBag<double>(); var watch = Stopwatch.StartNew();
            for (var round = 2; round <= 81; round++)
            {
                var requests = keys.Select(key => session.Request(key, round)).ToArray();
                await Parallel.ForEachAsync(requests, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (request, _) =>
                {
                    var start = Stopwatch.GetTimestamp(); Check(await session.RunAsync(request));
                    samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                });
            }
            watch.Stop(); process.Refresh();
            var ordered = samples.Order().ToArray();
            rows.Add(new { Kind = kind.ToString(), Enabled = enabled, Repetition = repetition, Commands = ordered.Length,
                WallMs = watch.Elapsed.TotalMilliseconds, CpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds,
                AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated,
                Gc0 = GC.CollectionCount(0) - gc[0], Gc1 = GC.CollectionCount(1) - gc[1], Gc2 = GC.CollectionCount(2) - gc[2],
                GcPauseMs = (GC.GetTotalPauseDuration() - pause).TotalMilliseconds,
                DatabaseActivities = Volatile.Read(ref databaseActivities) - dbStart,
                StatementActivities = Volatile.Read(ref statementActivities) - sqlStart,
                LockContentions = Monitor.LockContentionCount - contention, WorkingSetBytes = process.WorkingSet64, PeakWorkingSetBytes = process.PeakWorkingSet64,
                ManagedBytes = GC.GetTotalMemory(false), P50Ms = ordered[(int)(ordered.Length * .50)],
                P95Ms = ordered[(int)(ordered.Length * .95)], P99Ms = ordered[(int)(ordered.Length * .99)],
                session.PeakInFlight, session.ActiveInFlight, session.Loads, session.Statistics, ServerGC = GCSettings.IsServerGC });
            Console.WriteLine($"Resource sample {kind}: residency={enabled}, repetition={repetition}, {watch.Elapsed.TotalMilliseconds:F1} ms.");
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (soakSeconds <= 0) return;
        var sessions = new List<ResidentPilotSession>();
        try
        {
            foreach (var kind in Enum.GetValues<ResidentPilotKind>()) sessions.Add(await database.CreateSessionAsync(kind, true, 32));
            var stopwatch = Stopwatch.StartNew(); var round = 1; var samples = new List<object>();
            while (stopwatch.Elapsed.TotalSeconds < soakSeconds)
            {
                // A short burst followed by idle time exercises bounded residency without nonstop synthetic traffic.
                for (var burst = 0; burst < 4; burst++, round++)
                foreach (var session in sessions)
                {
                    var requests = Enumerable.Range(nextKey, 16).Select(key => session.Request(key, round)).ToArray();
                    await Parallel.ForEachAsync(requests, new ParallelOptions { MaxDegreeOfParallelism = 16 },
                        async (request, _) => Check(await session.RunAsync(request)));
                }
                database.ClearCallHistory();
                GC.Collect(); GC.WaitForPendingFinalizers(); process.Refresh();
                samples.Add(new { Seconds = stopwatch.Elapsed.TotalSeconds, ManagedBytes = GC.GetTotalMemory(true),
                    WorkingSetBytes = process.WorkingSet64, Actors = sessions.Select(s => new { Kind = s.Kind.ToString(), s.Loads, s.Statistics, s.PeakInFlight, s.ActiveInFlight }).ToArray() });
                await File.WriteAllTextAsync(output + ".soak.json", JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Resident soak: {stopwatch.Elapsed.TotalSeconds:F0}/{soakSeconds} seconds, round {round}, managed {GC.GetTotalMemory(false)}.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(20, Math.Max(0, soakSeconds - stopwatch.Elapsed.TotalSeconds))));
            }
            foreach (var session in sessions)
            foreach (var key in Enumerable.Range(nextKey, 16))
            {
                var request = session.Request(key, round - 1);
                if (await database.EventCountAsync(request.StreamId) != round - 1)
                    throw new InvalidOperationException("Soak source count differs from successful requests.");
            }
            await File.WriteAllTextAsync(output + ".soak-completed.json", JsonSerializer.Serialize(new
            {
                Completed = true, RequestedSeconds = soakSeconds, ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                Commands = (round - 1) * sessions.Count * 16, SourceStreamsVerified = sessions.Count * 16,
                Samples = samples.Count, Actors = sessions.Select(session => new
                { Kind = session.Kind.ToString(), session.ActiveInFlight, session.PeakInFlight, session.Statistics }).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { foreach (var session in sessions) await session.DisposeAsync(); }
    }

    static void Check(ResidentPilotMessage message)
    {
        if (message.Result?.Success != true) throw new InvalidOperationException(message.Result?.ErrorMessage ?? "Missing actor reply.");
    }
}
