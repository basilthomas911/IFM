using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Production diagnostic coalescing and the production API/Host codec over real NATS. A common admission barrier excludes artificial service delays.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceDiagnosticBenchmarks
{
    [Params(1, 8, 32)] public int Consumers { get; set; }
    readonly ApiServerPerformanceDatabaseFixture database = new();
    readonly CancellationTokenSource stopping = new();
    readonly NatsConnectionManager connections = new();
    NatsConnection server = null!;
    INatsSub<MarketDataHostRequest> subscription = null!;
    Task responder = null!;
    TaskCompletionSource admission = null!;
    BackendDiagnosticReads reads = null!;
    IMarketDataApi market = null!;
    int requests;
    string expectedSignature = null!;
    readonly MarketDataBackendStatus status = new(true, new(2026, 11, 6), []);

    [GlobalSetup]
    public async Task Setup()
    {
        await database.InitializeAsync();
        expectedSignature = MarketDataApiClient.Signature(typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.GetBackendStatusAsync))!);
        server = new(new NatsOpts { Url = database.Nats });
        await server.ConnectAsync();
        var connection = new MarketDataHostConnection(connections, database.Nats, "perf-" + Guid.NewGuid().ToString("N"));
        _ = await connection.GetClientAsync(default);
        subscription = await server.SubscribeCoreAsync<MarketDataHostRequest>(connection.ControlSubject("api/GetBackendStatusAsync"),
            serializer: NatsMessagePackSerializer<MarketDataHostRequest>.Default);
        await server.PingAsync();
        responder = RespondAsync();
        // The mirror is never used by this remote asynchronous operation; neither measured
        // path records mock invocations or retains a mock call history.
        market = new MarketDataApiClient(connection, Substitute.For<IMarketDataApi>());
        reads = new(market, Substitute.For<IHostApplicationLifetime>());
    }

    async Task RespondAsync()
    {
        try
        {
            await foreach (var request in subscription.Msgs.ReadAllAsync(stopping.Token))
            {
                await admission.Task.WaitAsync(stopping.Token);
                if (request.Data?.Path != "api/GetBackendStatusAsync"
                    || MarketDataHostConnection.Decode<MarketDataApiCall>(request.Data.Payload).Signature
                        != expectedSignature)
                    throw new InvalidOperationException("Diagnostic fixture request mismatch.");
                Interlocked.Increment(ref requests);
                await request.ReplyAsync(new MarketDataHostReply(MarketDataHostConnection.Encode(status), null),
                    serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default, cancellationToken: stopping.Token);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    async Task RunAsync(bool candidate)
    {
        admission = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = Volatile.Read(ref requests);
        var pending = Enumerable.Range(0, Consumers).Select(_ => candidate ? reads.StatusAsync(default) : market.GetBackendStatusAsync(default)).ToArray();
        admission.SetResult();
        var results = await Task.WhenAll(pending);
        if (results.Any(result => !result.Ready)) throw new InvalidOperationException("Diagnostic readiness changed.");
        var actual = Volatile.Read(ref requests) - before;
        if (actual != (candidate ? 1 : Consumers)) throw new InvalidOperationException($"Unexpected Host request count: {actual}.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("09-RealNatsDiagnosticCoalescing")]
    public Task IndependentBefore() => RunAsync(false);
    [Benchmark, BenchmarkCategory("09-RealNatsDiagnosticCoalescing")]
    public Task CoalescedAfter() => RunAsync(true);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await stopping.CancelAsync();
        if (responder is not null) await responder;
        if (subscription is not null) await subscription.DisposeAsync();
        await connections.DisposeAsync();
        if (server is not null) await server.DisposeAsync();
        stopping.Dispose(); await database.DisposeAsync();
    }
}
