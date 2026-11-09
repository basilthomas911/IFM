using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport.ResidentActors;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Actual actor and PostgreSQL before/after paths; baseline residency is explicitly disabled.</summary>
/// <remarks>Transport deserialization/NATS network and fixture setup are excluded. Both paths retain stream resolution,
/// command audit, calculation and required durable writes. Each same-stream caller awaits its preceding reply.</remarks>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public abstract class ResidentActorPilotBenchmarkBase
{
    [ParamsAllValues] public ResidentPilotKind Kind { get; set; }
    protected abstract string Workload { get; }
    protected virtual int StreamCount => 1;
    protected virtual int Capacity => 256;
    public int CommandsPerBatch => Workload switch { "Cold" => 1, "Replay" => 8, "Churn" => 32, _ => 2 * StreamCount };
    ResidentPilotDatabase database = null!;
    ResidentPilotSession before = null!, after = null!;
    ICommand[][] beforeRequests = null!, afterRequests = null!;
    int nextKey, beforeInitialLoads, afterInitialLoads;
    string? measuredSide;
    readonly List<object> counters = [];

    [GlobalSetup]
    public async Task Setup()
    {
        database = new ResidentPilotDatabase();
        await database.InitializeAsync().ConfigureAwait(false);
    }

    [IterationSetup]
    public void Prepare() => PrepareAsync().GetAwaiter().GetResult();

    public async Task PrepareAsync()
    {
        if (before is not null) await before.DisposeAsync().ConfigureAwait(false);
        if (after is not null) await after.DisposeAsync().ConfigureAwait(false);
        before = await database.CreateSessionAsync(Kind, false, Capacity).ConfigureAwait(false);
        after = await database.CreateSessionAsync(Kind, true, Capacity).ConfigureAwait(false);
        beforeRequests = new ICommand[StreamCount][];
        afterRequests = new ICommand[StreamCount][];
        for (var stream = 0; stream < StreamCount; stream++)
        {
            var beforeKey = nextKey++;
            var afterKey = nextKey++;
            var firstBefore = before.Request(beforeKey, 1);
            var firstAfter = after.Request(afterKey, 1);
            Check(await before.RunAsync(firstBefore).ConfigureAwait(false));
            Check(await after.RunAsync(firstAfter).ConfigureAwait(false));
            beforeRequests[stream] = Requests(before, beforeKey, firstBefore);
            afterRequests[stream] = Requests(after, afterKey, firstAfter);
        }
        if (Workload == "Cold")
        {
            await before.DisposeAsync().ConfigureAwait(false);
            await after.DisposeAsync().ConfigureAwait(false);
            before = await database.CreateSessionAsync(Kind, false).ConfigureAwait(false);
            after = await database.CreateSessionAsync(Kind, true).ConfigureAwait(false);
            before.Clock.Now = after.Clock.Now = database.Inputs.At(2);
        }
        beforeInitialLoads = before.Loads;
        afterInitialLoads = after.Loads;
        measuredSide = null;
        database.ClearCallHistory();
    }

    ICommand[] Requests(ResidentPilotSession session, int key, ICommand first)
    {
        if (Workload == "Cold") return [session.Request(key, 2)];
        if (Workload == "Replay")
            return Enumerable.Range(0, 8).Select(_ => Kind is ResidentPilotKind.Vwap or ResidentPilotKind.MarketOutlook
                ? session.Request(key, 1) : first).ToArray();
        return [session.Request(key, 2), session.Request(key, 3)];
    }

    protected async Task RunAsync(bool candidate)
    {
        measuredSide = candidate ? "After" : "Before";
        var session = candidate ? after : before;
        var requests = candidate ? afterRequests : beforeRequests;
        if (Workload == "Churn")
        {
            for (var round = 0; round < 2; round++)
                foreach (var stream in requests) Check(await session.RunAsync(stream[round]).ConfigureAwait(false));
            return;
        }
        await Parallel.ForEachAsync(requests, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (stream, _) =>
        {
            foreach (var request in stream) Check(await session.RunAsync(request).ConfigureAwait(false));
        }).ConfigureAwait(false);
    }

    static void Check(ResidentPilotMessage message)
    {
        if (message.Result?.Success != true)
            throw new InvalidOperationException("Pilot actor rejected benchmark input: " + message.Result?.ErrorMessage);
    }

    [IterationCleanup]
    public void VerifyCounters()
    {
        var candidate = measuredSide == "After";
        var session = candidate ? after : before;
        var loads = session.Loads - (candidate ? afterInitialLoads : beforeInitialLoads);
        var expected = candidate && (Workload is "Warm" or "Replay") ? 0 : CommandsPerBatch;
        if (loads != expected) throw new InvalidOperationException($"Unexpected {Workload} state loads: {loads}, expected {expected}.");
        counters.Add(new { Kind, Workload, Streams = StreamCount, CommandsPerBatch, Side = measuredSide, StateLoads = loads,
            session.Statistics, session.PeakInFlight, session.ActiveInFlight, Note = "State-load count excludes no-change/completion-replay head checks and durable writes." });
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (before is not null) await before.DisposeAsync().ConfigureAwait(false);
        if (after is not null) await after.DisposeAsync().ConfigureAwait(false);
        if (database is not null) await database.DisposeAsync().ConfigureAwait(false);
        var build = Environment.GetEnvironmentVariable("IFM_API_PERF_BUILD_ROOT");
        if (!string.IsNullOrEmpty(build))
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(build)!,
                $"resident-counters-{Kind}-{Workload}-{StreamCount}-{Environment.ProcessId}.json"),
                System.Text.Json.JsonSerializer.Serialize(counters)).ConfigureAwait(false);
    }
}

/// <summary>Cold actor state over an existing persisted checkpoint; identity resolution is included on both paths.</summary>
public class ResidentActorColdBenchmarks : ResidentActorPilotBenchmarkBase
{
    protected override string Workload => "Cold";
    [Benchmark(Baseline = true)] public Task Before() => RunAsync(false);
    [Benchmark] public Task After() => RunAsync(true);
}

/// <summary>Bounded independent streams, with two awaited commands per stream in each measured batch.</summary>
public class ResidentActorWarmBenchmarks : ResidentActorPilotBenchmarkBase
{
    [Params(1, 4, 16, 64)] public int Streams { get; set; }
    protected override string Workload => "Warm";
    protected override int StreamCount => Streams;
    [Benchmark(Baseline = true)] public Task Before() => RunAsync(false);
    [Benchmark] public Task After() => RunAsync(true);
}

/// <summary>Eight new-ID no-change Commands or eight exact duplicate Function requests, with authoritative head guards.</summary>
public class ResidentActorReplayBenchmarks : ResidentActorPilotBenchmarkBase
{
    protected override string Workload => "Replay";
    [Benchmark(Baseline = true)] public Task Before() => RunAsync(false);
    [Benchmark] public Task After() => RunAsync(true);
}

/// <summary>Sixteen streams and four cache entries deliberately force misses without exceeding admission limits.</summary>
public class ResidentActorChurnBenchmarks : ResidentActorPilotBenchmarkBase
{
    protected override string Workload => "Churn";
    protected override int StreamCount => 16;
    protected override int Capacity => 4;
    [Benchmark(Baseline = true)] public Task Before() => RunAsync(false);
    [Benchmark] public Task After() => RunAsync(true);
}
