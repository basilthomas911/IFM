using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Actor;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Snapshots;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class FinalRepositorySnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    private EventSourceActorDbContext Database => fixture.ActorEventDb;

    [Fact]
    public async Task Startup_restores_latest_report_and_release_tail()
    {
        var id = Guid.NewGuid();
        var run = new ParameterStartupRun(id, [], [], new(id, "fingerprint", "fingerprint", [], null), DateTime.UnixEpoch, "test");
        var report = new ParameterSignalStartupReport(id, run.Plan.Fingerprint, new(2026, 11, 3), "ES", DateTime.UnixEpoch, []);
        var command = new ApplySignalStartupPlanCommand
        {
            CommandId = Guid.NewGuid(), RunId = id, EntityId = new(id),
            Subject = new(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, id.ToString("N"))
        };
        var repository = new ParameterStartupStateRepository(Factory<ParameterStartupCommandState>(), Database, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ParameterStartupCommandActor>>(), NullLogger<ParameterStartupStateRepository>.Instance);
        var source = new List<IEvent> { new ParameterStartupCreatedEvent { EntityId = new(id), RunId = id, Revision = 1, Run = run } };
        source.AddRange(Enumerable.Range(2, 127).Select(i => new ParameterStartupChangedEvent
            { EntityId = new(id), RunId = id, Revision = i, Report = report }));
        source.Add(new ParameterStartupStateSnapshottedEvent { RunId = id, Revision = 128, ActiveRun = run, Report = report });
        source.Add(new ParameterStartupChangedEvent { EntityId = new(id), RunId = id, Revision = 129, Released = true });
        await Verify<ParameterStartupCommandState, ParameterStartupStateSnapshottedEvent>(command, repository, source, [1, 129, 130],
            (full, restored) =>
            {
                Assert.Equal(129, restored.Revision);
                Assert.Equal(full.RunId, restored.RunId);
                Assert.True(restored.Released);
                Assert.Empty(restored.ActiveRuns);
                Assert.Empty(restored.Reports);
            });
    }

    [Fact]
    public async Task Macd_restores_ordered_signal_history_and_a_later_signal()
    {
        var command = new GenerateFuturesMacdDailySignalCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new("ES", TimeFrameType.Daily, 26), Subject = Address("Macd")
        };
        var repository = new FuturesMacdSignalStateRepository(Factory<FuturesMacdSignalCommandState>(), Database,
            Substitute.For<IActorService>(), Substitute.For<IDbContextFactory>(),
            Substitute.For<IEventProjector<FuturesMacdSignalCommandActor>>(), NullLogger<FuturesMacdSignalStateRepository>.Instance);
        var signals = Enumerable.Range(1, 129).Select(i => new FuturesMacdSignalReadModel { ContractId = "ES", ObservationCount = i }).ToArray();
        var source = signals.Take(128).Select(signal => (IEvent)new FuturesMacdDailySignalGeneratedEvent { FuturesMacdSignal = signal }).ToList();
        source.Add(new FuturesMacdSignalStateSnapshottedEvent { Signals = signals[..128] });
        source.Add(new FuturesMacdDailySignalGeneratedEvent { FuturesMacdSignal = signals[^1] });
        await Verify<FuturesMacdSignalCommandState, FuturesMacdSignalStateSnapshottedEvent>(command, repository, source, [1, 129, 130],
            (full, restored) =>
            {
                Assert.Equal(129, restored.MacdSignals.Count);
                Assert.Equivalent(full.MacdSignals, restored.MacdSignals);
                Assert.Equal(129, restored.MacdSignal.ObservationCount);
            });
    }

    [Fact]
    public async Task Recovery_retains_original_fact_ids_for_duplicate_projection_repair()
    {
        var operation = Guid.NewGuid();
        var start = new RecoveryRecord
        {
            RecordId = Guid.NewGuid(), OperationId = operation, Sequence = 1, Kind = "Started", RuntimeId = "runtime",
            Role = "Options", Dataset = "GLBX.MDP3", ValueDate = new(2026, 11, 3), StartedAtUtc = DateTimeOffset.UnixEpoch,
            OccurredAtUtc = DateTimeOffset.UnixEpoch, RecoveryDeadlineUtc = DateTimeOffset.UnixEpoch.AddMinutes(2)
        };
        var command = Command(start);
        var repository = new MarketDataFeedRecoveryStateRepository(Factory<MarketDataFeedRecoveryState>(), Database,
            Substitute.For<IActorService>(), Substitute.For<IEventProjector<MarketDataFeedRecoveryCommandActor>>(),
            NullLogger<MarketDataFeedRecoveryStateRepository>.Instance);
        var records = Enumerable.Range(2, 128).Select(i => start with
        {
            RecordId = Guid.NewGuid(), Sequence = i, Kind = "ConnectionRestored",
            OccurredAtUtc = start.OccurredAtUtc.AddMilliseconds(i), ProviderActivityVerified = true
        }).Prepend(start).ToArray();
        var facts = records.Select((record, index) =>
        {
            MarketDataFeedRecoveryFact fact = index == 0 ? new MarketDataFeedRecoveryStartedEvent() : new MarketDataFeedConnectionRestoredEvent();
            var request = Command(record);
            return fact with { Id = record.RecordId, EntityId = request.EntityId, Payload = request.Payload,
                Digest = request.Digest, CommandId = request.CommandId, EventId = index + 1 };
        }).ToArray();
        await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
            new DomainEventCollection(facts.Take(127)), 0, CancellationToken.None);
        var lastCommand = Command(records[127]);
        var pending = await repository.LoadStateAsync(lastCommand);
        Assert.True(pending.Accept(lastCommand));
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var transactions = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, Database.ConnectionString, "System.Data.Postgres"));
        context.Container.Returns(container);
        container.Resolve<IPostgresEventTransaction>().Returns(transactions);
        await repository.SaveStateAsync(context, pending, lastCommand);
        Assert.Equal(129, pending.CommittedStreamVersion);
        var originalEventId = pending.FindFact(records[127].RecordId)!.EventId;
        Assert.True(originalEventId > 0);
        await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([facts[^1]]),
            129, CancellationToken.None);

        await Verify<MarketDataFeedRecoveryState, MarketDataFeedRecoveryStateSnapshottedEvent>(command, repository, [], [1, 129, 130],
            (full, restored) =>
            {
                Assert.Equal(129, restored.Latest!.Sequence);
                Assert.False(restored.Accept(Command(records[127])));
                Assert.Equal(originalEventId, restored.FindFact(records[127].RecordId)!.EventId);
                Assert.Equal(full.FindFact(records[127].RecordId)!.EventId, restored.FindFact(records[127].RecordId)!.EventId);
            }, savedHead: 130);

        AppendMarketDataFeedRecoveryCommand Command(RecoveryRecord record)
        {
            var payload = JsonSerializer.Serialize(record);
            return new AppendMarketDataFeedRecoveryCommand
            {
                CommandId = Guid.NewGuid(), EntityId = new(operation),
                Subject = new(ActorType.Command, AppendMarketDataFeedRecoveryCommand.Actor, "Record", operation.ToString("N")),
                Payload = payload, Digest = RecoveryRecord.Digest(payload), RequestDeadlineUtc = DateTime.UtcNow.AddMinutes(3)
            };
        }
    }

    [Fact]
    public async Task Workflow_restores_counters_and_the_committed_head()
    {
        var id = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "ES", new(2026, 11, 3), TimeFrameType.Daily));
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        var command = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(), Subject = Address("Workflow"), EntityId = id, ProposedWorkflowId = workflow
        };
        var databases = Substitute.For<IDbContextFactory>();
        var trade = (ITradeDbContext)Substitute.For([typeof(ITradeDbContext), typeof(IEnlistedTradeDbWriter)], []);
        trade.ProviderName.Returns("System.Data.Postgres");
        trade.ConnectionString.Returns(Database.ConnectionString);
        databases.TradeDb.Returns(trade);
        var transactions = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, Database.ConnectionString, "System.Data.Postgres"));
        var projector = Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        var repository = new IntrinsicTimeStrategyWorkflowStateRepository(Factory<IntrinsicTimeStrategyWorkflowCommandState>(),
            Database, Substitute.For<IActorService>(), projector, NullLogger<IntrinsicTimeStrategyWorkflowStateRepository>.Instance,
            databases, transactions);
        await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
            new DomainEventCollection(Enumerable.Range(1, 127).Select(Fact)), 0, CancellationToken.None);
        var pending = await repository.LoadStateAsync(command);
        Assert.True(pending.Update(Fact(128), command));
        await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), pending, command);
        Assert.Equal(129, pending.PersistedStreamVersion);
        Assert.Equal(129, pending.CommittedStreamVersion);
        await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([Fact(129)]),
            129, CancellationToken.None);
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            events => events.Count == 1 && events[0] is WorkflowStrategyStateUpdatedEvent));

        await Verify<IntrinsicTimeStrategyWorkflowCommandState, IntrinsicTimeStrategyWorkflowStateSnapshottedEvent>(
            command, repository, [], [1, 129, 130], (full, restored) =>
            {
                Assert.Equal(129, restored.AppliedEntityEventCount);
                Assert.Equal(130, restored.PersistedStreamVersion);
                Assert.Equal(StrategyWorkflowStartDecision.Accepted, restored.LastStartDecision);
                Assert.Equivalent(full.CurrentView, restored.CurrentView);
            }, resolveFromDb: true, savedHead: 130);

        WorkflowStrategyStateUpdatedEvent Fact(int revision) => new()
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id, WorkflowId = workflow, WorkflowRevision = revision,
            State = new() { EntityId = id, WorkflowId = workflow, WorkflowRevision = revision,
                Status = WorkflowStrategyMachineStatus.Started }
        };
    }

    [Fact]
    public async Task Live_feed_restores_latest_toggle_across_event_types()
    {
        var context = Substitute.For<IMarketDataFeedCommandContext>();
        var factory = Factory<MarketDataFeedCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<MarketDataFeedCommandActor>>();
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<MarketDataFeedCommandActor>.Instance);
        var repository = new MarketDataFeedStateRepository(context);
        var command = TestCommand(Address("Feed"));
        var source = Enumerable.Range(1, 128).Select(i =>
            i % 2 == 1 ? (IEvent)new TradeLiveFeedTurnedOffEvent() : new TradeLiveFeedTurnedOnEvent()).ToList();
        source.Add(new MarketDataFeedStoppedEvent());
        await Verify<MarketDataFeedCommandState, TradeLiveFeedTurnedOnEvent>(command, repository, source, [1, 128, 129],
            (full, restored) =>
            {
                Assert.True(restored.IsTradeLiveFeedOn);
                Assert.Equal(full.IsTradeLiveFeedOn, restored.IsTradeLiveFeedOn);
            });
    }

    [Fact]
    public Task FuturesBarData_restores_creation_and_head()
    {
        var context = Substitute.For<IFuturesBarDataCommandContext>();
        var factory = Factory<FuturesBarDataCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<FuturesBarDataCommandActor>>();
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<FuturesBarDataCommandActor>.Instance);
        return VerifyStateless(new FuturesBarDataStateRepository(context), () => new FuturesBarDataStreamingStoppedEvent());
    }

    [Fact]
    public Task FuturesEodData_restores_creation_and_head()
    {
        var context = Substitute.For<IFuturesEodDataCommandContext>();
        var factory = Factory<FuturesEodDataCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<FuturesEodDataCommandActor>>();
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<FuturesEodDataCommandActor>.Instance);
        return VerifyStateless(new FuturesEodDataStateRepository(context), () => new FuturesEodDataInsertedEvent());
    }

    [Fact]
    public Task FuturesTickData_restores_creation_and_head()
    {
        var context = Substitute.For<IFuturesTickDataCommandContext>();
        var factory = Factory<FuturesTickDataCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<FuturesTickDataCommandActor>>();
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<FuturesTickDataCommandActor>.Instance);
        return VerifyStateless(new FuturesTickDataStateRepository(context), () => new FuturesTickDataStreamingStoppedEvent());
    }

    [Fact]
    public Task FuturesOptionTickData_restores_creation_and_head()
    {
        var context = Substitute.For<IFuturesOptionTickDataCommandContext>();
        var factory = Factory<FuturesOptionTickDataCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<FuturesOptionTickDataCommandActor>>();
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<FuturesOptionTickDataCommandActor>.Instance);
        return VerifyStateless(new FuturesOptionTickDataStateRepository(context), () => new FuturesOptionTickDataStreamingStoppedEvent());
    }

    private async Task VerifyStateless<TState>(
        IEventSourceActorStateRepository<TState> repository, Func<IEvent> createFact)
        where TState : BaseEventSourceActorState<TState>, IStatelessCreationSnapshotState, new()
    {
        const int head = 64;
        var command = TestCommand(Address(typeof(TState).Name));
        await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
            new DomainEventCollection(Enumerable.Range(0, head).Select(_ => createFact())), 0, CancellationToken.None);
        var before = new List<double>();
        var after = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            if (i % 2 == 0)
            {
                await FullAsync();
                await SnapshotAsync();
            }
            else
            {
                await SnapshotAsync();
                await FullAsync();
            }
        }

        var rows = new List<long>();
        var stream = await Database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        await Database.MapReduceStatelessActorEventStreamFromCreationAsync<TState>(
            stream, page => rows.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, head }, rows);
        await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
            new DomainEventCollection([createFact()]), head, CancellationToken.None);
        Assert.Equal(head + 1, (await repository.LoadStateAsync(command)).CommittedStreamVersion);
        before.Sort();
        after.Sort();
        output.WriteLine($"{typeof(TState).Name}: {head} -> {rows.Count} rows, median " +
            $"{(before[9] + before[10]) / 2:F2} -> {(after[9] + after[10]) / 2:F2} ms, p95 {before[18]:F2} -> {after[18]:F2} ms.");

        async Task FullAsync()
        {
            var started = Stopwatch.GetTimestamp();
            var id = await Database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var state = new TState { Id = command.Subject.ThreadId };
            await Database.MapReduceActorEventStreamFromCreationAsync<TState>(id, state.ReplayEvents);
            before.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.Equal(head, state.CommittedStreamVersion);
        }

        async Task SnapshotAsync()
        {
            var started = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            after.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.Equal(head, state.CommittedStreamVersion);
            Assert.Empty(state.Events);
        }
    }

    [Fact]
    public async Task Closing_price_restores_the_latest_insert()
    {
        var context = Substitute.For<IFuturesClosingPriceCommandContext>();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesClosingPriceCommandState>().Returns(_ => new FuturesClosingPriceCommandState());
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(Substitute.For<IActorService>());
        context.EventProjector.Returns(Substitute.For<IEventProjector<FuturesClosingPriceCommandActor>>());
        context.Logger.Returns(NullLogger<FuturesClosingPriceCommandActor>.Instance);
        var repository = new FuturesClosingPriceStateRepository(context);
        var command = TestCommand(Address("ClosingPrice"));
        var first = new TomasAI.IFM.Domain.MarketData.Shared.FuturesDataId("ES", new(2026, 10, 1));
        var last = new TomasAI.IFM.Domain.MarketData.Shared.FuturesDataId("ES", new(2026, 10, 2));
        var source = Enumerable.Range(1, 128).Select(i => (IEvent)new FuturesClosingPriceInsertedEvent
        {
            EntityId = i == 128 ? last : first, ClosingPrice = 5000m + i
        }).ToArray();

        await Verify<FuturesClosingPriceCommandState, FuturesClosingPriceInsertedEvent>(command, repository, source,
            [1, 128], (full, restored) =>
            {
                Assert.True(full.FuturesClosingPriceExists(last));
                Assert.True(restored.FuturesClosingPriceExists(last));
                Assert.False(restored.FuturesClosingPriceExists(first));
            }, createState: () => new FuturesClosingPriceCommandState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Regime_configuration_retains_publication_on_retirement(bool historical)
    {
        var key = Guid.NewGuid();
        var id = new RegimeDiscoveryParameterSetEntityId(key, 1);
        var parameters = RegimeDiscoveryParameterSet.CreateDefault(key, key, TimeFrameType.Daily);
        var command = TestCommand(Address("RegimeConfiguration"));
        var repository = new RegimeDiscoveryConfigurationStateRepository(Factory<RegimeDiscoveryConfigurationCommandState>(),
            Database, Substitute.For<IActorService>(), Substitute.For<IEventProjector<RegimeDiscoveryConfigurationCommandActor>>(),
            NullLogger<RegimeDiscoveryConfigurationStateRepository>.Instance);
        await Verify<RegimeDiscoveryConfigurationCommandState, RegimeDiscoveryConfigurationParameterSetRetiredEvent>(command, repository,
            [new RegimeDiscoveryConfigurationParameterSetCreatedEvent { EntityId = id, ParameterSet = parameters },
                new RegimeDiscoveryConfigurationParameterSetPublishedEvent { EntityId = id, EffectiveFromUtc = DateTime.UnixEpoch },
                new RegimeDiscoveryConfigurationParameterSetRetiredEvent { EntityId = id, RetiredAtUtc = DateTime.UnixEpoch.AddDays(1),
                    ParameterSet = historical ? null : parameters, EffectiveFromUtc = historical ? null : DateTime.UnixEpoch }],
            [1, 3], (full, restored) =>
            {
                Assert.Equal("Retired", restored.Status);
                Assert.Equal(full.EffectiveFromUtc, restored.EffectiveFromUtc);
                Assert.Equal(full.RetiredAtUtc, restored.RetiredAtUtc);
                Assert.Equivalent(full.ParameterSet, restored.ParameterSet);
            }, fallbackRows: historical ? 3 : 0);
    }

    [Fact]
    public async Task Download_attempt_uses_its_single_terminal_creation_fact()
    {
        var context = Substitute.For<IDownloadLogCommandContext>();
        var factory = Factory<DownloadLogCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<DownloadLogCommandActor>>();
        context.DbEventSource.Returns(Database);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<DownloadLogCommandActor>.Instance);
        var repository = new DownloadLogStateRepository(context);
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = MarketDataDownloadDataset.EconomicCalendar, Scope = "US", ValueDate = new(2026, 11, 3),
            ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(), RequestedAtUtc = DateTime.UnixEpoch,
            StartedAtUtc = DateTime.UnixEpoch.AddSeconds(1), FinishedAtUtc = DateTime.UnixEpoch.AddSeconds(2),
            Status = MarketDataDownloadStatus.Completed, DownloadedRecordCount = 3, PersistedRecordCount = 3,
            ElapsedMilliseconds = 1000
        };
        var command = new InsertMarketDataDownloadLogCommand(outcome);
        await Verify<DownloadLogCommandState, MarketDataDownloadLogInsertedEvent>(command, repository,
            [new MarketDataDownloadLogInsertedEvent { Outcome = outcome, EntityId = command.EntityId,
                CommandId = command.CommandId, PayloadSha256 = command.PayloadSha256 }], [1],
            (_, restored) => Assert.True(restored.VerifyDuplicate(command)));
    }

    [Fact]
    public async Task Broker_order_restores_observation_hashes_for_old_and_new_observations()
    {
        var order = new TradeOrderId(1, 1, 1);
        var id = new BrokerOrderId(new(order, Guid.NewGuid()), Guid.NewGuid());
        var command = TestCommand(Address("BrokerOrder"));
        var repository = new BrokerOrderStateRepository(Factory<BrokerOrderCommandState>(), Database,
            Substitute.For<IActorService>(), Substitute.For<IEventProjector<BrokerOrderCommandActor>>(),
            NullLogger<BrokerOrderStateRepository>.Instance);
        var observations = Enumerable.Range(1, 129).Select(i => new BrokerOrderObservationEvidence
            { ObservationId = Guid.NewGuid(), ContentHash = $"observation-{i}" }).ToArray();
        var source = Enumerable.Range(1, 128).Select(i => (IEvent)Fact(i)).ToList();
        source.Add(new BrokerOrderStateSnapshottedEvent { Definition = Fact(128).BrokerOrderDefinition,
            ObservationHashes = observations.Take(128).ToDictionary(value => value.ObservationId, value => value.ContentHash) });
        source.Add(Fact(129));
        await Verify<BrokerOrderCommandState, BrokerOrderStateSnapshottedEvent>(command, repository, source, [1, 129, 130],
            (full, restored) =>
            {
                Assert.Equivalent(full.BrokerOrderDefinition, restored.BrokerOrderDefinition);
                foreach (var observation in observations)
                {
                    Assert.True(restored.TryGetObservationHash(observation.ObservationId, out var hash));
                    Assert.Equal(observation.ContentHash, hash);
                }
            });

        BrokerOrderChangedEvent Fact(int revision) => new()
        {
            EntityId = id, BrokerOrderDefinition = new() { Id = id, Revision = revision, LastObservation = observations[revision - 1] }
        };
    }

    private async Task Verify<TState, TSnapshot>(ICommand command, IEventSourceActorStateRepository<TState> repository,
        IReadOnlyList<IEvent> facts, long[] expectedRows, Action<TState, TState> assertState, bool resolveFromDb = false, int? savedHead = null, int fallbackRows = 0, Func<TState>? createState = null)
        where TState : BaseEventSourceActorState<TState> where TSnapshot : IEvent
    {
        var identified = facts.Select(fact =>
        {
            if (fact.Id == Guid.Empty) EventInitHelper.SetProperty(fact, nameof(IEvent.Id), Guid.NewGuid());
            if (fact.CommandId == Guid.Empty) EventInitHelper.SetProperty(fact, nameof(IEvent.CommandId), Guid.NewGuid());
            return fact;
        });
        if (savedHead is null)
            await Database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(identified), 0, CancellationToken.None);
        var head = savedHead ?? facts.Count;
        var before = new double[20];
        var after = new double[20];
        for (var i = 0; i < before.Length; i++)
        {
            TState baseline;
            TState restored;
            if (i % 2 == 0)
            {
                baseline = await Full(i);
                restored = await Snapshot(i);
            }
            else
            {
                restored = await Snapshot(i);
                baseline = await Full(i);
            }

            Assert.Equal(head, restored.CommittedStreamVersion);
            Assert.Empty(restored.Events);
            assertState(baseline, restored);
        }
        var stream = await Database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        var rows = new List<long>();
        await Database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, [typeof(TSnapshot)], page => rows.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(expectedRows, rows);
        Array.Sort(before);
        Array.Sort(after);
        output.WriteLine($"{typeof(TState).Name}: {head} -> {rows.Count + fallbackRows} rows, median " +
            $"{(before[9] + before[10]) / 2:F2} -> {(after[9] + after[10]) / 2:F2} ms, p95 {before[18]:F2} -> {after[18]:F2} ms.");

        async Task<TState> Full(int sample)
        {
            var started = Stopwatch.GetTimestamp();
            var id = resolveFromDb
                ? (await Database.GetEventStreamIdFromDbAsync(command.StreamId))!.EventStreamId
                : await Database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var baseline = createState is null ? Activator.CreateInstance<TState>() : createState();
            baseline.Id = command.Subject.ThreadId;
            await Database.MapReduceActorEventStreamFromCreationAsync<TState>(id, baseline.ReplayEvents);
            before[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return baseline;
        }

        async Task<TState> Snapshot(int sample)
        {
            var started = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            after[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return state;
        }
    }

    private static ActorSubject Address(string owner) =>
        new(ActorType.Command, "FinalRepositorySnapshotTests", owner, Guid.NewGuid().ToString("N"));

    private static ICommand TestCommand(ActorSubject subject)
    {
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(subject);
        command.StreamId.Returns(subject.StreamId);
        return command;
    }

    private static IEventSourceActorStateFactory Factory<TState>() where TState : BaseEventSourceActorState<TState>, new()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        return factory;
    }
}
