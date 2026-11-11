using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.EventSourcing.Command;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.TestSupport.ResidentActors;

/// <summary>Disposable PostgreSQL/NATS fixtures shared by integration tests and actor-level measurements.</summary>
public sealed class ResidentPilotDatabase(string? existingTestConnection = null) : IAsyncDisposable
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("residentpilots", includeCql: false);
    string connectionString = string.Empty;
    public string TestConnectionString => connectionString;
    readonly List<object> substitutes = [];
    readonly Dictionary<Type, object> repositories = [];
    public ResidentPilotInputs Inputs { get; } = new();
    public EventSourceActorDbContext Events { get; private set; } = null!;
    public MarketDataDbContext Market { get; private set; } = null!;
    public TradePlanDbContext Plans { get; private set; } = null!;
    public IDbContextFactory Databases { get; private set; } = null!;
    public ResidentPilotTransactions Transactions { get; private set; } = null!;
    public IEventSourceActorStateFactory States { get; private set; } = null!;
    public IContainerInstance Container { get; private set; } = null!;
    public IActorSupervisor Supervisor { get; private set; } = null!;
    public string NatsUrl => infrastructure.NatsUrl;

    public async Task InitializeAsync()
    {
        try
        {
            if (existingTestConnection is null)
            {
                await infrastructure.StartAsync().ConfigureAwait(false);
                connectionString = infrastructure.PostgresConnectionString;
            }
            else
            {
                var supplied = new NpgsqlConnectionStringBuilder(existingTestConnection);
                if (supplied.Database?.StartsWith("ifm_residentpilots_", StringComparison.Ordinal) != true
                    || supplied.Host is not ("127.0.0.1" or "localhost") || supplied.Port == 5432)
                    throw new InvalidOperationException("Child process qualification requires an isolated resident-pilot PostgreSQL endpoint.");
                connectionString = supplied.ConnectionString;
            }
            var settings = new DbConnectionSettings()
                .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connectionString, "System.Data.Postgres")
                .Add(MarketDataDbContext.MarketDataDbConnection, connectionString, "System.Data.Postgres")
                .Add(TradePlanDbContext.TradePlanDbConnection, connectionString, "System.Data.Postgres");
            await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync().ConfigureAwait(false);
            await using (var connection = await OpenAsync().ConfigureAwait(false))
            {
                await using var command = new NpgsqlCommand(MarketDataSchemaSql.Create, connection);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            await new TradePlanSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync().ConfigureAwait(false);
            Databases = new DbContextFactory(new DbContextResolver(type => repositories[type]));

            var values = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();


            Events = new(settings, Databases, NullLogger<DbProvider>.Instance);
            repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), Events);
            Market = new(settings, Databases, Fake<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
            repositories.Add(typeof(IObjectRepository<MarketDataDbContext>), Market);
            Plans = new(settings, NullLogger<DbProvider>.Instance);
            repositories.Add(typeof(IObjectRepository<TradePlanDbContext>), Plans);
            Transactions = new(new PostgresEventTransaction(settings));
            States = Fake<IEventSourceActorStateFactory>();
            States.CreateState<FuturesVwapSignalCommandState>().Returns(_ => new FuturesVwapSignalCommandState());
            States.CreateState<MarketOutlookSnapshotCommandState>().Returns(_ => new MarketOutlookSnapshotCommandState());
            States.CreateState<FuturesTradePlanFunctionState>().Returns(_ => new FuturesTradePlanFunctionState());
            States.CreateState<VerticalSpreadTradePlanFunctionState>().Returns(_ => new VerticalSpreadTradePlanFunctionState());
            States.CreateState<IronCondorTradePlanFunctionState>().Returns(_ => new IronCondorTradePlanFunctionState());
            Container = Fake<IContainerInstance>();
            Container.Resolve<IDbContextFactory>().Returns(Databases);
            Container.Resolve<IPostgresEventTransaction>().Returns(Transactions);
            Container.Resolve<ICommandAuditLogger>().Returns(new CommandAuditCoordinator(Events));
            Supervisor = Fake<IActorSupervisor>();
            Supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(_ => Fake<IActorMailbox>());
            Supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Fake<IActorProducer>());
        }
        catch { await DisposeAsync().ConfigureAwait(false); throw; }
    }

    T Fake<T>() where T : class
    {
        var value = Substitute.For<T>();
        substitutes.Add(value);
        return value;
    }

    public void ClearCallHistory()
    {
        foreach (var value in substitutes) value.ClearReceivedCalls();
    }

    public async Task<ResidentPilotSession> CreateSessionAsync(ResidentPilotKind kind, bool enabled, int capacity = 256)
    {
        var options = new ResidentActorPilotOptions();
        foreach (var policy in new[] { options.Vwap, options.MarketOutlook, options.FuturesTradePlan,
                     options.VerticalSpreadTradePlan, options.IronCondorTradePlan })
        { policy.Enabled = enabled; policy.MaximumResidentStreams = capacity; }
        var clock = new ResidentPilotClock(Inputs.Epoch);
        var actors = Fake<IActorService>();
        switch (kind)
        {
            case ResidentPilotKind.Vwap:
            {
                var context = Fake<IFuturesVwapSignalCommandContext>();
                context.ActorId.Returns(new ActorMailboxId(ActorType.Command, "FuturesVwapSignalCommand"));
                context.Container.Returns(Container);
                context.DbEventSource.Returns(Events);
                context.Logger.Returns(NullLogger<FuturesVwapSignalCommandActor>.Instance);
                var projection = Fake<IEventProjector<FuturesVwapSignalCommandActor>>();
                context.EventProjector.Returns(projection);
                var repository = new CountingResidentCommandRepository<FuturesVwapSignalCommandState>(
                    new FuturesVwapSignalStateRepository(States, Events, actors, projection, NullLogger<FuturesVwapSignalStateRepository>.Instance));
                context.StateRepository.Returns(repository);
                var actor = new FuturesVwapSignalCommandActor(context, options);
                await actor.StartAsync(Supervisor).ConfigureAwait(false);
                return new(kind, Inputs, clock, actor.HandleMessageAsync, actor.StopAsync,
                    () => repository.Loads, () => actor.CommittedCacheStatistics, repository);
            }
            case ResidentPilotKind.MarketOutlook:
            {
                var context = Fake<IMarketOutlookSnapshotCommandContext>();
                context.ActorId.Returns(new ActorMailboxId(ActorType.Command, "MarketOutlookSnapshotCommand"));
                context.Container.Returns(Container);
                context.Logger.Returns(NullLogger<MarketOutlookSnapshotCommandActor>.Instance);
                var projection = Fake<IEventProjector<MarketOutlookSnapshotCommandActor>>();
                context.EventProjector.Returns(projection);
                var repository = new CountingResidentCommandRepository<MarketOutlookSnapshotCommandState>(
                    new MarketOutlookSnapshotStateRepository(States, Events, actors, projection, NullLogger<MarketOutlookSnapshotStateRepository>.Instance));
                context.Repository.Returns(repository);
                var actor = new MarketOutlookSnapshotCommandActor(context, options);
                await actor.StartAsync(Supervisor).ConfigureAwait(false);
                return new(kind, Inputs, clock, actor.HandleMessageAsync, actor.StopAsync,
                    () => repository.Loads, () => actor.CommittedCacheStatistics, repository);
            }
            case ResidentPilotKind.FuturesPlan:
            {
                var repository = new CountingResidentFunctionRepository<FuturesTradePlanFunctionState, UpdateFuturesTradePlanCommand>(
                    new FuturesTradePlanFunctionStateRepository(States, Events, actors, Fake<IEventProjector<FuturesTradePositionCommandActor>>(),
                        NullLogger<FuturesTradePlanFunctionStateRepository>.Instance, Databases, Transactions));
                var context = Fake<IFuturesTradePlanFunctionContext>();
                context.ActorId.Returns(new ActorMailboxId(ActorType.Function, UpdateFuturesTradePlanCommand.Actor));
                context.Container.Returns(Container); context.TimeProvider.Returns(clock);
                context.Logger.Returns(NullLogger<FuturesTradePlanFunctionActor>.Instance);
                context.Algorithm.Returns(new FuturesTradePlanAlgorithm()); context.StateRepository.Returns(repository);
                var actor = new FuturesTradePlanFunctionActor(context, options);
                await actor.StartAsync(Supervisor).ConfigureAwait(false);
                return new(kind, Inputs, clock, actor.HandleMessageAsync, actor.StopAsync,
                    () => repository.Loads, () => actor.ResidentCacheStatistics, repository);
            }
            case ResidentPilotKind.VerticalSpreadPlan:
            {
                var repository = new CountingResidentFunctionRepository<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand>(
                    new VerticalSpreadTradePlanFunctionStateRepository(States, Events, actors, Fake<IEventProjector<FuturesVerticalSpreadTradePositionCommandActor>>(),
                        NullLogger<VerticalSpreadTradePlanFunctionStateRepository>.Instance, Databases, Transactions));
                var context = Fake<IVerticalSpreadTradePlanFunctionContext>();
                context.ActorId.Returns(new ActorMailboxId(ActorType.Function, UpdateVerticalSpreadTradePlanCommand.Actor));
                context.Container.Returns(Container); context.TimeProvider.Returns(clock);
                context.Logger.Returns(NullLogger<VerticalSpreadTradePlanFunctionActor>.Instance);
                context.Algorithm.Returns(new VerticalSpreadTradePlanAlgorithm()); context.StateRepository.Returns(repository);
                var actor = new VerticalSpreadTradePlanFunctionActor(context, options);
                await actor.StartAsync(Supervisor).ConfigureAwait(false);
                return new(kind, Inputs, clock, actor.HandleMessageAsync, actor.StopAsync,
                    () => repository.Loads, () => actor.ResidentCacheStatistics, repository);
            }
            default:
            {
                var repository = new CountingResidentFunctionRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand>(
                    new IronCondorTradePlanFunctionStateRepository(States, Events, actors, Fake<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(),
                        NullLogger<IronCondorTradePlanFunctionStateRepository>.Instance, Databases, Transactions));
                var context = Fake<IIronCondorTradePlanFunctionContext>();
                context.ActorId.Returns(new ActorMailboxId(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor));
                context.Container.Returns(Container); context.TimeProvider.Returns(clock);
                context.Logger.Returns(NullLogger<IronCondorTradePlanFunctionActor>.Instance);
                context.Algorithm.Returns(new IronCondorTradePlanAlgorithm()); context.StateRepository.Returns(repository);
                var actor = new IronCondorTradePlanFunctionActor(context, options);
                await actor.StartAsync(Supervisor).ConfigureAwait(false);
                return new(kind, Inputs, clock, actor.HandleMessageAsync, actor.StopAsync,
                    () => repository.Loads, () => actor.ResidentCacheStatistics, repository);
            }
        }
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString)
        { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return connection;
    }

    public async Task<long> EventCountAsync(string stream)
    {
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM event_log l JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1;", connection);
        command.Parameters.AddWithValue(stream);
        return (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    public async ValueTask DisposeAsync()
    {
        if (Events is not null) await ((IAsyncDisposable)Events).DisposeAsync().ConfigureAwait(false);
        if (existingTestConnection is null) await infrastructure.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>Controls one actual actor instance without replacing its dispatch/validation/calculation code.</summary>
public sealed class ResidentPilotSession(ResidentPilotKind kind, ResidentPilotInputs inputs, ResidentPilotClock clock,
    Func<IActorMessage, ActorThreadId, CancellationToken, ValueTask> dispatch, Func<ValueTask> stop,
    Func<int> loads, Func<ResidentActorCacheStatistics> statistics, object repository) : IAsyncDisposable
{
    int active, peak;
    public int ActiveInFlight => Volatile.Read(ref active);
    public int PeakInFlight => Volatile.Read(ref peak);
    public ResidentPilotKind Kind => kind;
    public int Loads => loads();
    public ResidentActorCacheStatistics Statistics => statistics();
    public object Repository => repository;
    public ResidentPilotClock Clock => clock;
    public ICommand Request(int key, long revision)
    {
        clock.Now = inputs.At(revision);
        return inputs.Create(kind, key, revision);
    }

    public async ValueTask<ResidentPilotMessage> RunAsync(ICommand request, CancellationToken token = default,
        Func<ValueTask>? beforeReply = null)
    {
        var message = new ResidentPilotMessage(request) { BeforeReply = beforeReply };
        var count = Interlocked.Increment(ref active);
        var previous = Volatile.Read(ref peak);
        while (count > previous)
        {
            var observed = Interlocked.CompareExchange(ref peak, count, previous);
            if (observed == previous) break;
            previous = observed;
        }
        try { await dispatch(message, request.Subject.ThreadId, token).ConfigureAwait(false); }
        finally { Interlocked.Decrement(ref active); }
        return message;
    }

    public ValueTask DispatchAsync(IActorMessage message, CancellationToken token = default)
        => dispatch(message, message.Subject.ThreadId, token);

    public ValueTask DisposeAsync() => stop();
}

public sealed class CountingResidentCommandRepository<TState>(IResidentCommandStateRepository<TState> inner)
    : IResidentCommandStateRepository<TState> where TState : IEventSourceActorState<TState>
{
    int loads;
    public int Loads => Volatile.Read(ref loads);
    public ValueTask<TState> LoadStateAsync(ICommand command) => LoadStateAsync(command, default);
    public ValueTask<TState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
    { Interlocked.Increment(ref loads); return inner.LoadStateAsync(command, cancellationToken); }
    public ValueTask SaveStateAsync(ICommandActorContext context, TState state, ICommand command)
        => inner.SaveStateAsync(context, state, command);
    public ValueTask SaveStateAsync(ICommandActorContext context, TState state, ICommand command, CancellationToken cancellationToken)
        => inner.SaveStateAsync(context, state, command, cancellationToken);
    public ValueTask ValidateNoChangeAsync(TState state, ICommand command, CancellationToken cancellationToken)
        => inner.ValidateNoChangeAsync(state, command, cancellationToken);
}

public sealed class CountingResidentFunctionRepository<TState, TRequest>(IResidentFunctionStateRepository<TState, TRequest> inner)
    : IResidentFunctionStateRepository<TState, TRequest> where TState : IActorState where TRequest : ICommand
{
    int loads;
    public int Loads => Volatile.Read(ref loads);
    public ValueTask<TState> LoadStateAsync(TRequest request, CancellationToken cancellationToken = default)
    { Interlocked.Increment(ref loads); return inner.LoadStateAsync(request, cancellationToken); }
    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context, TState state, TRequest request, CancellationToken cancellationToken = default)
        => inner.SaveCompletedStateAsync(context, state, request, cancellationToken);
    public ValueTask<bool> IsCurrentAsync(TState state, TRequest request, CancellationToken cancellationToken)
        => inner.IsCurrentAsync(state, request, cancellationToken);
}

/// <summary>Injects a failure after all business writes but before commit, without changing production persistence.</summary>
public sealed class ResidentPilotTransactions(IPostgresEventTransaction inner) : IPostgresEventTransaction
{
    public bool FailNextCommit { get; set; }
    public TaskCompletionSource? CommitGate { get; set; }
    public TaskCompletionSource Entered { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => inner.ExecuteOnceAsync((transaction, token) => RunAsync(operation, transaction, token), cancellationToken);
    public Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => inner.ExecuteAsync((transaction, token) => RunAsync(operation, transaction, token), cancellationToken);

    async Task<T> RunAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        EnlistedEventTransaction transaction, CancellationToken token)
    {
        var value = await operation(transaction, token).ConfigureAwait(false);
        Entered.TrySetResult();
        if (CommitGate is { } gate) await gate.Task.ConfigureAwait(false);
        if (FailNextCommit) { FailNextCommit = false; throw new InvalidOperationException("Injected rollback before commit."); }
        return value;
    }
}
