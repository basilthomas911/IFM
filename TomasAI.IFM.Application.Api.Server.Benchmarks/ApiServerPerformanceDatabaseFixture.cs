using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Uses the existing isolated infrastructure owner; no Development endpoint or CQL service is used.</summary>
internal sealed class ApiServerPerformanceDatabaseFixture : IAsyncDisposable
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("apiperformancebench", includeCql: false);
    int disposed;
    IRedisCache cache = null!;
    IEventSourceActorStateFactory stateFactory = null!;
    IEventProjector<FuturesEmaSignalCommandActor> projector = null!;
    readonly List<object> additionalProjectors = [];
    public IDbContextFactory Databases { get; private set; } = null!;
    public EventSourceActorDbContext Events { get; private set; } = null!;
    public FuturesEmaSignalStateRepository Ema { get; private set; } = null!;
    public MarketDataDbContext Market { get; private set; } = null!;
    public ICommandActorContext Context { get; private set; } = null!;
    public string Redis => infrastructure.RedisConnectionString;
    public string Nats => infrastructure.NatsUrl;
    public string AuthenticatedPostgres => new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
        { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;

    public async Task InitializeAsync()
    {
        try
        {
            await infrastructure.StartAsync();
            var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
                infrastructure.PostgresConnectionString, "System.Data.Postgres")
                .Add(MarketDataDbContext.MarketDataDbConnection, infrastructure.PostgresConnectionString, "System.Data.Postgres");
            var logger = NullLogger<DbProvider>.Instance;
            await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
            await using (var connection = new NpgsqlConnection(AuthenticatedPostgres))
            {
                await connection.OpenAsync();
                await using var schema = new NpgsqlCommand(MarketDataSchemaSql.Create, connection);
                await schema.ExecuteNonQueryAsync();
            }
            var repositories = new Dictionary<Type, object>();
            var databases = new DbContextFactory(new DbContextResolver(type => repositories[type]));
            Databases = databases;
            cache = Substitute.For<IRedisCache>();
            var values = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            cache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(call =>
            { var found = values.TryGetValue(call.ArgAt<string>(0), out var value); call[1] = value!; return found; });
            cache.When(instance => instance.Set(Arg.Any<string>(), Arg.Any<string>())).Do(call => values[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
            var blackboard = new BlackboardService(cache, new SystemTextJsonSerializer());
            Events = new(settings, databases, blackboard, logger);
            repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), Events);
            Market = new(settings, databases, blackboard, Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(), logger);
            repositories.Add(typeof(IObjectRepository<MarketDataDbContext>), Market);
            stateFactory = Substitute.For<IEventSourceActorStateFactory>();
            stateFactory.CreateState<FuturesEmaSignalCommandState>().Returns(_ => new FuturesEmaSignalCommandState());
            projector = Substitute.For<IEventProjector<FuturesEmaSignalCommandActor>>();
            Ema = new(stateFactory, Events, Substitute.For<IActorService>(), projector, NullLogger<FuturesEmaSignalStateRepository>.Instance);
            var factory = Substitute.For<IDbContextFactory>();
            var market = Substitute.For<IMarketDataDbContext>();
            market.ProviderName.Returns("System.Data.Postgres");
            market.ConnectionString.Returns(Events.ConnectionString); factory.MarketDataDb.Returns(market);
            Context = Substitute.For<ICommandActorContext>(); var container = Substitute.For<IContainerInstance>();
            Context.Container.Returns(container); container.Resolve<IDbContextFactory>().Returns(factory);
            container.Resolve<IPostgresEventTransaction>().Returns(new PostgresEventTransaction(settings));
            var root = Path.GetDirectoryName(Environment.GetEnvironmentVariable("IFM_API_PERF_BUILD_ROOT")
                ?? @"E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\generated")!;
            await File.WriteAllTextAsync(Path.Combine(root, "infrastructure-" + infrastructure.RunId + ".json"), System.Text.Json.JsonSerializer.Serialize(new
            { infrastructure.RunId, infrastructure.PostgresPort, infrastructure.NatsUrl, infrastructure.RedisConnectionString, infrastructure.CqlPort,
                Note = "Disposable synthetic infrastructure only; credentials resolved through the existing test environment. CQL disabled." }));
        }
        catch { await DisposeAsync(); throw; }
    }

    public void ClearCallHistory()
    {
        cache.ClearReceivedCalls(); stateFactory.ClearReceivedCalls(); projector.ClearReceivedCalls();
        foreach (var additional in additionalProjectors) additional.ClearReceivedCalls();
    }

    public TRepository CreateIndicatorRepository<TRepository, TState, TActor>()
        where TRepository : class
        where TState : class, IEventSourceActorState<TState>, new()
        where TActor : ICommandActor<TActor>
    {
        stateFactory.CreateState<TState>().Returns(_ => new TState());
        var projection = Substitute.For<IEventProjector<TActor>>();
        additionalProjectors.Add(projection);
        return (TRepository)(Activator.CreateInstance(typeof(TRepository), stateFactory, Events,
            Substitute.For<IActorService>(), Databases, projection, NullLogger<TRepository>.Instance)
            ?? throw new InvalidOperationException("Indicator repository construction failed."));
    }

    public TomasAI.IFM.Application.Storage.TradeDb.TradeFeedRecoveryStore CreateTradeRecoveryStore()
        => new(new DbConnectionSettings().Add(TomasAI.IFM.Application.Storage.TradeDb.TradeDbContext.TradeDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres"));

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (Events is not null) await Events.DisposeAsync();
        await infrastructure.DisposeAsync();
    }
}
