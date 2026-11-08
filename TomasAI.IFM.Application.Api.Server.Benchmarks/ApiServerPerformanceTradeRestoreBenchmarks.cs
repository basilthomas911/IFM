using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Original/current API startup services, production Host protocol, recovery service and PostgreSQL intent store. Native subscription admission completes immediately; this does not time native discovery or option pricing.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceTradeRestoreBenchmarks
{
    [Params(1, 4, 32, 128)] public int Trades { get; set; }
    readonly ApiServerPerformanceDatabaseFixture database = new();
    readonly NatsConnectionManager connections = new();
    readonly CancellationTokenSource stopping = new();
    readonly DateOnly date = new(2026, 11, 6);
    NatsConnection server = null!;
    INatsSub<MarketDataHostRequest> subscription = null!;
    Task responder = null!;
    IMarketDataApi backend = null!, client = null!;
    TradeFeedRecoveryService after = null!;
    ApiServerPerformanceTradeRecoveryBaseline before = null!;
    Store store = null!;
    Runtime runtime = null!;
    volatile bool candidate;
    readonly Guid generation = Guid.NewGuid();

    [GlobalSetup]
    public async Task Setup()
    {
        await database.InitializeAsync();
        await using (var connection = new NpgsqlConnection(database.AuthenticatedPostgres))
        {
            await connection.OpenAsync();
            await using var schema = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS trade; CREATE TABLE IF NOT EXISTS trade.live_feed_recovery (trade_id text NOT NULL,value_date date NOT NULL,contract_id text NOT NULL,updated_at timestamptz NOT NULL,PRIMARY KEY(trade_id,value_date,contract_id));", connection);
            await schema.ExecuteNonQueryAsync();
        }
        store = new(database.CreateTradeRecoveryStore()); runtime = new(date);
        for (var index = 0; index < Trades; index++)
        {
            var id = new TradeEntityId(101, 701, 1901, 1501 + index);
            var definition = new EstablishedTradeDefinition { Id = id, Status = EstablishedTradeStatus.Open, StrategyKind = TradeStrategyKind.IronCondor,
                Legs = Enumerable.Range(0, 4).Select(leg => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = $"option-{index}-{leg}", SignedQuantity = leg % 2 == 0 ? -1 : 1 }).ToArray() };
            runtime.Definitions.Add(id, definition);
            await store.SaveAsync(new(id.Format(), date, definition.Legs.Select(leg => leg.ContractId).Order(StringComparer.Ordinal).ToArray(), DateTimeOffset.UtcNow), default);
        }
        backend = Substitute.For<IMarketDataApi>();
        backend.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, date, [], generation));
        backend.ReadTradeFeedRecoveryAsync(date, Arg.Any<CancellationToken>()).Returns(call => candidate ? after.ReadAsync(date, call.Arg<CancellationToken>()) : before.ReadAsync(date, call.Arg<CancellationToken>()));
        backend.LoadTradeFeedRecoveryAsync(Arg.Any<string>(), date, Arg.Any<CancellationToken>()).Returns(call => candidate
            ? after.LoadAsync(call.Arg<string>(), date, false, call.Arg<CancellationToken>()) : before.LoadAsync(call.Arg<string>(), date, false, call.Arg<CancellationToken>()));
        server = new(new NatsOpts { Url = database.Nats }); await server.ConnectAsync();
        var connectionToHost = new MarketDataHostConnection(connections, database.Nats, "restore-" + Guid.NewGuid().ToString("N"));
        _ = await connectionToHost.GetClientAsync(default);
        subscription = await server.SubscribeCoreAsync<MarketDataHostRequest>(connectionToHost.Scope + ".*", serializer: NatsMessagePackSerializer<MarketDataHostRequest>.Default);
        await server.PingAsync();
        var dispatcher = new MarketDataApiDispatcher(backend);
        responder = RespondAsync(dispatcher);
        client = new MarketDataApiClient(connectionToHost, Substitute.For<IMarketDataApi>());
    }

    [IterationSetup]
    public void Prepare()
    {
        backend.ClearReceivedCalls(); store.Writes = 0; runtime.Starts = 0;
        after = new(store, runtime); before = new(store, runtime);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("19-NatsPostgresSavedTradeRestoration")]
    public async Task SerialBefore()
    {
        candidate = false;
        using var startup = new ApiServerPerformanceTradeStartupBaseline(client, Sessions(), Supervisor(), Substitute.For<IJSActorProducer>(), NullLogger<ApiServerPerformanceTradeStartupBaseline>.Instance);
        await startup.StartAsync(default);
        try { await WaitAsync(() => startup.Current?.Restored == true); }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await startup.StopAsync(stop.Token); }
        if (store.Writes != Trades || runtime.Starts != Trades * 4) throw new InvalidOperationException("Baseline restoration counts differ.");
    }

    [Benchmark, BenchmarkCategory("19-NatsPostgresSavedTradeRestoration")]
    public async Task BoundedAfter()
    {
        candidate = true;
        using var startup = new TradeFeedRecoveryStartupService(client, Sessions(), Supervisor(), Substitute.For<IJSActorProducer>(), NullLogger<TradeFeedRecoveryStartupService>.Instance);
        await startup.StartAsync(default);
        try { await WaitAsync(() => startup.Current?.Restored == true); }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await startup.StopAsync(stop.Token); }
        if (store.Writes != 0 || runtime.Starts != Trades * 4) throw new InvalidOperationException("Candidate restoration duplicated intent writes or lost subscriptions.");
    }

    async Task RespondAsync(MarketDataApiDispatcher dispatcher)
    {
        try
        {
            await Parallel.ForEachAsync(subscription.Msgs.ReadAllAsync(stopping.Token), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stopping.Token },
                async (request, token) =>
                {
                    var call = MarketDataHostConnection.Decode<MarketDataApiCall>(request.Data!.Payload);
                    var payload = await dispatcher.InvokeAsync(call, token);
                    await request.ReplyAsync(new MarketDataHostReply(payload, null), serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default, cancellationToken: token);
                });
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }
    IFuturesMarketSessionAuthority Sessions() { var sessions = Substitute.For<IFuturesMarketSessionAuthority>(); sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date }); return sessions; }
    static IActorSupervisor Supervisor() { var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); return supervisor; }
    static async Task WaitAsync(Func<bool> restored)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!restored()) await Task.Delay(1, timeout.Token);
    }
    [GlobalCleanup]
    public async Task Cleanup()
    {
        await stopping.CancelAsync(); if (responder is not null) await responder;
        if (subscription is not null) await subscription.DisposeAsync();
        await connections.DisposeAsync(); if (server is not null) await server.DisposeAsync();
        stopping.Dispose(); await database.DisposeAsync();
    }

    sealed class Store(ITradeFeedRecoveryStore inner) : ITradeFeedRecoveryStore
    {
        public int Writes;
        public Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date, CancellationToken token) => inner.ReadAsync(date, token);
        public Task SaveAsync(TradeFeedRecoveryRegistration registration, CancellationToken token) { Interlocked.Increment(ref Writes); return inner.SaveAsync(registration, token); }
        public Task RemoveAsync(string trade, DateOnly date, CancellationToken token) => inner.RemoveAsync(trade, date, token);
        public Task CloseAsync(DateOnly date, CancellationToken token) => inner.CloseAsync(date, token);
    }
    sealed class Runtime(DateOnly date) : ITradeFeedRecoveryRuntime, IApiServerPerformanceTradeFeedRecoveryRuntime
    {
        public Dictionary<TradeEntityId, EstablishedTradeDefinition> Definitions { get; } = [];
        public int Starts;
        public DateOnly? ActiveDate => date;
        public Task<EstablishedTradeDefinition?> LoadAsync(TradeEntityId trade, CancellationToken token) => Task.FromResult<EstablishedTradeDefinition?>(Definitions[trade]);
        public Task StartAsync(string owner, string contract, DateOnly valueDate, CancellationToken token) { Interlocked.Increment(ref Starts); return Task.CompletedTask; }
        public Task StopAsync(string owner, string contract, CancellationToken token) => Task.CompletedTask;
    }
}
