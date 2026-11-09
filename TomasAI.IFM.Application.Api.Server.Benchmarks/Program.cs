using BenchmarkDotNet.Running;

if (args.Contains("--verify-api-performance-warmup", StringComparer.Ordinal))
{
    foreach (var scenario in new[] { "Stale", "Retry", "Independent", "SameKey" })
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceWarmupBenchmarks { Scenario = scenario };
        fixture.Prepare(); await fixture.OriginalBefore(); await fixture.CurrentAfter();
        Console.WriteLine($"Original/current warmup qualification passed: {scenario}.");
    }
    return;
}


if (args.Contains("--api-performance-seed-resource-probe", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceSeedReaderBenchmarks { Trades = 300000 };
    await fixture.Setup();
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await fixture.ResourceProbeAsync(args.Contains("--candidate", StringComparer.Ordinal))));
    return;
}

if (args.Contains("--verify-api-performance-diagnostic", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceDiagnosticBenchmarks { Consumers = 32 };
    try { await fixture.Setup(); await fixture.IndependentBefore(); await fixture.CoalescedAfter(); Console.WriteLine("Production API/Host codec: 32 independent requests versus one shared request, with no per-call test double."); }
    finally { await fixture.Cleanup(); }
    return;
}

if (args.Contains("--verify-api-performance-next-wave", StringComparer.Ordinal))
{
    var indicators = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceIndicatorCommandBenchmarks();
    try
    {
        await indicators.Setup(); indicators.Prepare(); await indicators.SerialBefore(); indicators.VerifyWarmModels();
        indicators.Prepare(); await indicators.BoundedAfter(); indicators.VerifyWarmModels();
        Console.WriteLine("Five real indicator commands persisted warm projections on both paths.");
    }
    finally { await indicators.Cleanup(); }
    foreach (var count in new[] { 1, 4, 32, 128 })
    {
        var restoration = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceTradeRestoreBenchmarks { Trades = count };
        try
        {
            await restoration.Setup(); restoration.Prepare(); await restoration.SerialBefore();
            restoration.Prepare(); await restoration.BoundedAfter();
            Console.WriteLine($"Production NATS/PostgreSQL restoration fixture passed: {count} trades, unchanged contracts and no redundant intent writes.");
        }
        finally { await restoration.Cleanup(); }
    }
    var reader = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceSeedReaderBenchmarks { Trades = 100000 };
    await reader.Setup(); reader.Reset(); _ = await reader.ReadBefore(); reader.Reset(); _ = await reader.ReadAfter();
    Console.WriteLine("Full seed reader fixture passed: identical 48 bars through materialized and streaming paths.");
    return;
}

if (args.Contains("--verify-api-performance-fixtures", StringComparer.Ordinal))
{
    foreach (var bars in new[] { 21, 48, 64 })
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceEmaPersistenceBenchmarks { Bars = bars };
        try
        {
            await fixture.Setup();
            fixture.Prepare(); await fixture.PerBarBefore(); fixture.VerifyPersistedRows();
            fixture.Prepare(); await fixture.AtomicBatchAfter(); fixture.VerifyPersistedRows();
            Console.WriteLine($"PostgreSQL EMA fixture passed: {bars} bars, per-bar and atomic batch.");
        }
        finally { await fixture.Cleanup(); }
    }
    var diagnostics = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceDiagnosticBenchmarks { Consumers = 32 };
    try
    {
        await diagnostics.Setup(); await diagnostics.IndependentBefore(); await diagnostics.CoalescedAfter();
        Console.WriteLine("NATS diagnostic fixture passed: 32 requests before, one shared request after.");
    }
    finally { await diagnostics.Cleanup(); }
    var indicatorReads = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceIndicatorReadBenchmarks();
    try
    {
        await indicatorReads.Setup(); await indicatorReads.SerialBefore(); await indicatorReads.BoundedAfter();
        Console.WriteLine("PostgreSQL indicator fixture passed: serial and bounded restoration reads.");
    }
    finally { await indicatorReads.Cleanup(); }
    var redis = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceRedisBenchmarks();
    try
    {
        await redis.Setup(); redis.SynchronousBefore(); await redis.AsynchronousAfter();
        Console.WriteLine("Redis readiness fixture passed: synchronous and asynchronous connections.");
    }
    finally { await redis.Cleanup(); }
    return;
}

if (args.Contains("--api-performance-resource-probe", StringComparer.Ordinal))
{
    var probe = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceTradeContentionBenchmarks
    { Producers = int.Parse(args[Array.IndexOf(args, "--producers") + 1], System.Globalization.CultureInfo.InvariantCulture) };
    try
    {
        probe.Setup();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(probe.ResourceProbe(args.Contains("--candidate", StringComparer.Ordinal))));
    }
    finally { probe.Cleanup(); }
    return;
}

if (args.Contains("--verify-api-optimizations", StringComparer.Ordinal))
{
    var verification = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiHotPathOptimizationBenchmarks();
    try { verification.Setup(); verification.VerifyEquivalence(); Console.WriteLine("Optimization equivalence checks passed (1,000 randomized snapshots)."); }
    finally { verification.Cleanup(); }
    return;
}
if (args.Contains("--verify-resident-pilots", StringComparer.Ordinal))
{
    foreach (var kind in Enum.GetValues<TomasAI.IFM.TestSupport.ResidentActors.ResidentPilotKind>())
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorWarmBenchmarks { Kind = kind, Streams = 64 };
        try
        {
            await fixture.Setup(); await fixture.PrepareAsync(); await fixture.Before(); fixture.VerifyCounters();
            await fixture.PrepareAsync(); await fixture.After(); fixture.VerifyCounters();
            Console.WriteLine($"Resident actor paired fixture passed: {kind}, 64 streams, 128 awaited commands per path.");
        }
        finally { await fixture.Cleanup(); }
    }
    return;
}
if (args.Contains("--resident-resource-probe", StringComparer.Ordinal))
{
    var output = args[Array.IndexOf(args, "--output") + 1];
    var seconds = args.Contains("--soak-seconds", StringComparer.Ordinal)
        ? int.Parse(args[Array.IndexOf(args, "--soak-seconds") + 1], System.Globalization.CultureInfo.InvariantCulture) : 0;
    await TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorResourceProbe.RunAsync(output, seconds);
    return;
}
if (args.Contains("--resident-child-restart", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorProcessRestartProbe.RunAsync(true,
        Enum.Parse<TomasAI.IFM.TestSupport.ResidentActors.ResidentPilotKind>(args[1]),
        int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
    return;
}
if (args.Contains("--verify-resident-process-restart", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorProcessRestartProbe.RunAsync(false);
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.ActorHealthJsonBenchmarks).Assembly).Run(args);
