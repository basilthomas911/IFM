using NSubstitute;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresWorkflowSnapshotTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Assigned_queue_pages_exact_contract_and_signal_order()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], token));
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], token));
        var at = new DateTime(2026, 11, 15, 14, 30, 0, DateTimeKind.Utc);
        var settings = new DbConnectionSettings().Add("TradeDbConnection", fixture.Connection, "System.Data.Postgres");
        var databases = Substitute.For<IDbContextFactory>();
        var trade = new TradeDbContext(settings, databases, Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(),
            NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance);
        databases.TradeDb.Returns(trade);
        var catalogKind = TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind.Deployment;
        var routes = new IntrinsicTimeFundAssignment[]
        {
            new(1, 10, "Daily Futures", "Future", "ES", TimeFrameType.Daily, new(catalogKind, Guid.NewGuid(), 1), 1, at.AddDays(-1), null),
            new(1, 11, "Weekly Verticals", "Vertical", "ES", TimeFrameType.Weekly, new(catalogKind, Guid.NewGuid(), 1), 1, at.AddDays(-1), null),
            new(1, 12, "Monthly Condors", "IronCondor", "ES", TimeFrameType.Monthly, new(catalogKind, Guid.NewGuid(), 1), 1, at.AddDays(-1), null)
        };
        var queryContext = Substitute.For<IIntrinsicTimeStrategyWorkflowQueryContext>();
        queryContext.DbFactory.Returns(databases);
        queryContext.QueueAssignments.Returns(routes);

        foreach (var route in routes)
        {
            var older = await Add("ES20261218", route, 10, at.AddMinutes(5));
            var newer = await Add("ES20261218", route, 11, at.AddMinutes(1));
            var other = await Add("ES20270319", route, 12, at.AddMinutes(2));
            var legacy = await Add("ES20261218", route, 9, at.AddSeconds(-10), legacy: true);
            await Add("ES20270618", route, 8, at.AddSeconds(-20), legacy: true);
            await Add("ES20261218", route with { FundId = 999 }, 13, at.AddMinutes(2));
            var request = new GetIntrinsicTimeStrategyUpdatesGridQuery
            {
                Symbol = "ES", TimePeriod = route.Timeframe, FromUtc = at.AddMinutes(-1), ToUtc = at.AddMinutes(10),
                ContractId = "ES20261218", AssignmentKey = route.QueueSuffix(), PageSize = 1
            };
            var first = (await request.ExecuteAsync(queryContext, CancellationToken.None)).Value!;
            var head = Assert.Single(first.Items);
            Assert.Equal(newer, head.WorkflowId);
            Assert.True(first.HasMore);
            var next = request with { PageNumber = 2, BeforeSignalAtUtc = head.OccurredAtUtc,
                BeforeSignalSequence = head.SignalSequence, BeforeWorkflowId = head.WorkflowId.Value };
            var second = (await next.ExecuteAsync(queryContext, CancellationToken.None)).Value!;
            var secondRow = Assert.Single(second.Items);
            Assert.Equal(older, secondRow.WorkflowId);
            Assert.False(secondRow.LegacyUnassigned);
            Assert.True(second.HasMore);
            var third = (await (next with { PageNumber = 3, BeforeSignalAtUtc = secondRow.OccurredAtUtc,
                BeforeSignalSequence = secondRow.SignalSequence, BeforeWorkflowId = secondRow.WorkflowId.Value })
                .ExecuteAsync(queryContext, CancellationToken.None)).Value!;
            var legacyRow = Assert.Single(third.Items);
            Assert.Equal(legacy, legacyRow.WorkflowId);
            Assert.True(legacyRow.LegacyUnassigned);
            Assert.Equal(at.AddSeconds(-10), legacyRow.HistoryOrderAtUtc);
            Assert.Equal(at, legacyRow.OccurredAtUtc);
            Assert.False(third.HasMore);
            var afterLegacy = (await (next with { PageNumber = 4, BeforeSignalAtUtc = legacyRow.HistoryOrderAtUtc,
                BeforeSignalSequence = legacyRow.SignalSequence, BeforeWorkflowId = legacyRow.WorkflowId.Value })
                .ExecuteAsync(queryContext, CancellationToken.None)).Value!;
            Assert.Empty(afterLegacy.Items);
            Assert.Equal(other, Assert.Single((await (request with { ContractId = "ES20270319" })
                .ExecuteAsync(queryContext, CancellationToken.None)).Value!.Items).WorkflowId);
            Assert.Equal("NoTrade", head.Outcome);
        }

        var schema = new TradeSchemaDb(settings, NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance);
        await schema.CreateAsync(["intrinsic_time_strategy_updates_queue"]);
        await schema.CreateAsync(["intrinsic_time_strategy_updates_queue"]);

        var catalog = (await new GetIntrinsicTimeStrategyUpdatesGridQuery { QueueCatalog = true }
            .ExecuteAsync(queryContext, CancellationToken.None)).Value!;
        Assert.Equal(routes, catalog.Assignments);
        Assert.Equal(new[] { "ES20261218", "ES20270319", "ES20270618" }, catalog.Contracts);

        async Task<StrategyWorkflowId> Add(string contract, IntrinsicTimeFundAssignment assignment, long sequence, DateTime started, bool legacy = false)
        {
            var signalEntity = new FuturesItiSignalEntityId(contract, DateOnly.FromDateTime(at), assignment.Timeframe);
            var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(signalEntity) with { Assignment = legacy ? null : assignment };
            var trigger = Guid.NewGuid();
            var workflow = StrategyWorkflowId.ForTrigger(trigger, entity, at);
            Assert.Equal(workflow, StrategyWorkflowId.ForTrigger(trigger, entity, at));
            Assert.Equal(7, workflow.Value.Version);
            var state = new IntrinsicTimeStrategyWorkflowView
            {
                EntityId = entity, WorkflowId = workflow, TriggerEventId = trigger, WorkflowRevision = 1,
                TriggerEvent = new() { Id = trigger, EntityId = signalEntity, EventId = sequence, CreatedOn = at },
                StartedAtUtc = started, UpdatedAtUtc = started, TerminalAtUtc = started,
                Status = WorkflowStrategyMachineStatus.Completed, Outcome = StrategyWorkflowOutcome.NoTrade
            };
            var source = new WorkflowStrategyStateUpdatedEvent
            {
                Id = Guid.NewGuid(), EventId = sequence, WorkflowId = workflow, EntityId = entity, WorkflowRevision = 1,
                WorkflowDefinition = state, UpdatedAtUtc = started
            };
            await fixture.Transactions.ExecuteOnceAsync(async (db, token) => { await trade.ProjectWorkflowAsync(db, source, token); return true; });
            await fixture.Transactions.ExecuteOnceAsync(async (db, token) => { await trade.ProjectWorkflowAsync(db, source, token); return true; });
            if (legacy)
                await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(
                    "UPDATE trade.intrinsic_time_strategy_workflow SET signalatutc = NULL, signalsequence = 0 WHERE workflowid = $1",
                    [workflow.Value], token));
            return workflow;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queued_signals_survive_checkpoint_and_each_get_one_strategy_update(bool recoverAfterDeadline)
    {
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], token));
        var now = new DateTime(2026, 11, 13, 14, 30, 0, DateTimeKind.Utc);
        var symbol = recoverAfterDeadline ? "ZB" : "ZN";
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(new(symbol + "Z26", DateOnly.FromDateTime(now), TimeFrameType.Daily));
        if (recoverAfterDeadline)
            entity = entity with { Assignment = new(1, 50, "Futures", "Future", symbol, TimeFrameType.Daily,
                new(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind.Deployment, Guid.NewGuid(), 1), 1,
                now.AddDays(-1), null) };
        var clock = new QueueTestClock(now);
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowCommandActor>.Instance);
        context.TimeProvider.Returns(clock);
        var options = new RegimeDiscoveryExecutionOptions { TriggerMaximumAgeSecondsByTimeFrame = { [TimeFrameType.Daily] = 20 } };
        context.ExecutionOptions.Returns(options);
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, fixture.Connection, "System.Data.Postgres")
            .Add("TradeDbConnection", fixture.Connection, "System.Data.Postgres");
        var databases = Substitute.For<IDbContextFactory>();
        var trade = new TradeDbContext(settings, databases, Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(),
            NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance);
        databases.TradeDb.Returns(trade);
        var source = new EventSourceActorDbContext(settings, databases, NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance);
        databases.ActorEventSourceDb.Returns(source);
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<IntrinsicTimeStrategyWorkflowCommandState>().Returns(_ => new IntrinsicTimeStrategyWorkflowCommandState());
        var projector = Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        var actors = Substitute.For<IActorService>();
        var repository = new IntrinsicTimeStrategyWorkflowStateRepository(stateFactory, source, actors, projector,
            NullLogger<IntrinsicTimeStrategyWorkflowStateRepository>.Instance, databases, fixture.Transactions);
        context.StateRepository.Returns(repository);
        ICommandActor<IntrinsicTimeStrategyWorkflowCommandActor> actor = new IntrinsicTimeStrategyWorkflowCommandActor(context);
        var queryContext = Substitute.For<IIntrinsicTimeStrategyWorkflowQueryContext>();
        queryContext.DbFactory.Returns(databases);
        var first = Signal(now.AddSeconds(-2));
        var second = Signal(now.AddSeconds(-1));
        var third = Signal(now.AddSeconds(1));
        await Start(first);
        var firstStart = (await repository.LoadStateAsync(first)).LatestSource!;
        var marker = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync(
            "SELECT count(*) FROM event_projector_state WHERE eventid=$1 AND projectorname=$2;",
            [firstStart.EventId, firstStart.RequiredProjection.ProjectorName], token));
        Assert.Equal(1L, marker);
        clock.Now = now.AddSeconds(1);
        await Start(second);
        // Reach the production checkpoint boundary while the second signal is still waiting.
        var fillerId = Guid.NewGuid();
        var filler = new DomainEventCollection(Enumerable.Range(0, 125).Select(_ => firstStart with
        {
            Id = Guid.NewGuid(), CommandId = fillerId, EventId = 0, DispatchRegimeDiscovery = false
        }));
        await source.SaveEventsAsync(first.StreamId, fillerId, filler, 2, CancellationToken.None);
        await Start(third);
        await Start(Retry(second));
        var queued = await repository.LoadStateAsync(first);
        Assert.Equal(second.TriggerEventId, queued.NextSignal!.TriggerEventId);
        Assert.Equal(first.ProposedWorkflowId, queued.CurrentView!.WorkflowId);
        Assert.Single((await ReadGrid()).Items);
        Assert.Equal(129, queued.PersistedStreamVersion);
        Assert.Equal(129, await fixture.EventCountAsync(first.StreamId));

        if (recoverAfterDeadline)
        {
            clock.Now = now.AddMinutes(3);
            actors.RequestAsync<RedispatchCurrentStrategyPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                Arg.Any<RedispatchCurrentStrategyPipelineCommand>(), Arg.Any<CancellationToken>())
                .Returns(call => Wake(call.ArgAt<RedispatchCurrentStrategyPipelineCommand>(0)));
            using var recovery = new IntrinsicTimeSignalQueueRecoveryService(fixture.Transactions, actors, clock,
                NullLogger<IntrinsicTimeSignalQueueRecoveryService>.Instance);
            await recovery.RunOnceAsync(CancellationToken.None);
            await recovery.RunOnceAsync(CancellationToken.None);
            await actors.Received(1).RequestAsync<RedispatchCurrentStrategyPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                Arg.Any<RedispatchCurrentStrategyPipelineCommand>(), Arg.Any<CancellationToken>());
        }
        else
        {
            clock.Now = now.AddSeconds(20);
            var failure = new FailRegimeDiscoveryCommand
            {
                CommandId = Guid.NewGuid(), EntityId = entity, WorkflowId = first.ProposedWorkflowId,
                InputWorkflowRevision = 1, SourceEventId = Guid.NewGuid(),
                Subject = new(ActorType.Command, FailRegimeDiscoveryCommand.Actor, FailRegimeDiscoveryCommand.Verb, entity.Format())
            };
            await Handle(failure);
        }

        var beforeRetries = await fixture.EventCountAsync(first.StreamId);
        foreach (var signal in new[] { first, second, third })
        {
            await Start(signal);
            await Start(Retry(signal));
        }
        Assert.Equal(beforeRetries, await fixture.EventCountAsync(first.StreamId));
        Assert.Null((await repository.LoadStateAsync(first)).NextSignal);

        var grid = await ReadGrid();
        Assert.Equal(3, grid.TotalCount);
        foreach (var item in grid.Items)
        {
            var detail = await trade.GetIntrinsicTimeStrategyWorkflowViewAsync(item.WorkflowId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Equal(MessagePackBinarySerializer.SerializeHistoricalContent(IntrinsicTimeStrategyUpdatesGridRow.FromView(detail)),
                MessagePackBinarySerializer.SerializeHistoricalContent(item));
        }
        Assert.Equal(new[] { first.ProposedWorkflowId, second.ProposedWorkflowId, third.ProposedWorkflowId }.OrderBy(id => id.Value),
            grid.Items.Select(item => item.WorkflowId).OrderBy(id => id.Value));
        var stale = Assert.Single(grid.Items, item => item.WorkflowId == second.ProposedWorkflowId);
        Assert.Equal(second.TriggerEvent.CreatedOn, stale.OccurredAtUtc);
        Assert.Equal("Completed", stale.Status);
        Assert.Equal("NoTrade", stale.Outcome);
        Assert.Equal("Stop", stale.Stages[0].Continuation);
        Assert.Contains("RD.TRIGGER.STALE", stale.Stages[0].DecisionReasons!);
        Assert.Contains("limit 20 seconds", stale.Stages[0].ReasonSentence);
        Assert.Equal("NotStarted", stale.Stages[1].Processing);
        Assert.Equal(recoverAfterDeadline ? "NoTrade" : "None",
            Assert.Single(grid.Items, item => item.WorkflowId == third.ProposedWorkflowId).Outcome);
        var started = await trade.GetIntrinsicTimeStrategyWorkflowTimelineAsync(second.ProposedWorkflowId, 0, 10);
        var revisions = started.OrderBy(row => row.WorkflowRevision).ToArray();
        Assert.Equal(new long[] { 1, 2 }, revisions.Select(row => row.WorkflowRevision));
        Assert.True(MessagePack.MessagePackSerializer.Deserialize<WorkflowStrategyStateUpdatedEvent>(revisions[0].EventPayload).SkipPipelineDispatch);
        var thirdTimeline = await trade.GetIntrinsicTimeStrategyWorkflowTimelineAsync(third.ProposedWorkflowId, 0, 10);
        Assert.True(revisions[1].EventId < thirdTimeline.Min(row => row.EventId));

        ExecuteIntrinsicTimeStrategyWorkflowCommand Signal(DateTime signalAt)
        {
            var id = Guid.NewGuid();
            return new()
            {
                CommandId = Guid.NewGuid(), EntityId = entity, ProposedWorkflowId = new(Guid.NewGuid()),
                Subject = new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, entity.Format()),
                TriggerEventId = id, TriggerEvent = new() { Id = id, EntityId = entity.ItiSignalEntityId, CreatedOn = signalAt, ReceivedOn = signalAt },
                RequestedAtUtc = clock.Now, CorrelationId = id, CausationId = id, WorkflowDefinitionVersion = 1
            };
        }
        static ExecuteIntrinsicTimeStrategyWorkflowCommand Retry(ExecuteIntrinsicTimeStrategyWorkflowCommand signal) =>
            signal with { CommandId = Guid.NewGuid(), ProposedWorkflowId = new(Guid.NewGuid()) };
        ValueTask Start(ExecuteIntrinsicTimeStrategyWorkflowCommand signal) => Handle(signal);
        async ValueTask<ServiceResult<Guid>> Wake(RedispatchCurrentStrategyPipelineCommand wake)
        {
            await Handle(wake);
            return new ServiceResult<Guid>(wake.CommandId);
        }
        async ValueTask Handle(ICommand command)
        {
            var loaded = await actor.OnLoadStateAsync(context, command.Subject.ThreadId, command);
            var result = await actor.ReceiveAsync(context, loaded, command);
            Assert.True(result.Success, result.ErrorMessage);
            await actor.OnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);
        }
        async Task<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels.IntrinsicTimeStrategyUpdatesGridPage> ReadGrid()
        {
            var query = new GetIntrinsicTimeStrategyUpdatesGridQuery
            {
                Symbol = symbol, TimePeriod = TimeFrameType.Daily, FromUtc = now.AddMinutes(-1), ToUtc = now.AddMinutes(5)
            };
            var result = await query.ExecuteAsync(queryContext, CancellationToken.None);
            Assert.True(result.Success);
            return result.Value!;
        }
    }

    sealed class QueueTestClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    [Theory]
    [InlineData("System.Data.Postgres", true)]
    [InlineData("System.Data.ScyllaDb", false)]
    public void Workflow_actor_reserves_audit_at_the_same_boundary_as_its_repository(string provider, bool atomic)
    {
        var database = Substitute.For<ITradeDbContext>();
        database.ProviderName.Returns(provider);
        var factory = Substitute.For<IDbContextFactory>();
        factory.TradeDb.Returns(database);
        var context = new IntrinsicTimeStrategyWorkflowCommandContext(
            Substitute.For<IActorSupervisor>(), factory,
            new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options.RegimeDiscoveryExecutionOptions(),
            new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor.IntrinsicTimeStrategyWorkflowOptions(),
            NullLogger<IntrinsicTimeStrategyWorkflowCommandActor>.Instance);
        var actor = new IntrinsicTimeStrategyWorkflowCommandActor(context);
        var boundary = typeof(IntrinsicTimeStrategyWorkflowCommandActor).GetMethod("AuditIsCommittedWithState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(atomic, (bool)boundary.Invoke(actor, [new ExecuteIntrinsicTimeStrategyWorkflowCommand()])!);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Command_repository_owns_atomic_rows_and_notifies_only_after_commit(bool failAfterWrite, bool failNotification)
    {
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], token));
        var now = DateTime.UtcNow;
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(new("repository-" + Guid.NewGuid().ToString("N"), DateOnly.FromDateTime(now), TimeFrameType.Monthly));
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        var command = new ExecuteIntrinsicTimeStrategyWorkflowCommand { CommandId = Guid.NewGuid(), EntityId = entity,
            Subject = new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, entity.Format()), ProposedWorkflowId = workflow };
        var source = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), CommandId = command.CommandId, EntityId = entity,
            WorkflowId = workflow, WorkflowRevision = 1, ReceivedOn = now, UpdatedAtUtc = now,
            State = new() { EntityId = entity, WorkflowId = workflow, WorkflowRevision = 1, Status = WorkflowStrategyMachineStatus.Started,
                CurrentStage = StrategyWorkflowStage.RegimeDiscovery, StartedAtUtc = now, UpdatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(2) } };
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(source, addEvent: true).Should().BeTrue();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        eventSource.ProviderName.Returns("System.Data.Postgres");
        eventSource.ConnectionString.Returns(fixture.Connection);
        var target = Substitute.For<ITradeDbContext, IEnlistedTradeDbWriter>();
        target.ProviderName.Returns("System.Data.Postgres");
        target.ConnectionString.Returns(fixture.Connection);
        ((IEnlistedTradeDbWriter)target).ProjectWorkflowAsync(Arg.Any<IEnlistedPostgresTransaction>(), Arg.Any<WorkflowStrategyStateUpdatedEvent>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await TradeDbContextExtensions.WorkflowProjectAsync(call.ArgAt<IEnlistedPostgresTransaction>(0), call.ArgAt<WorkflowStrategyStateUpdatedEvent>(1), call.ArgAt<CancellationToken>(2));
                if (failAfterWrite) throw new InvalidOperationException("Injected repository projection failure.");
            });
        var databases = Substitute.For<IDbContextFactory>();
        databases.TradeDb.Returns(target);
        var projector = Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        async Task Notify()
        {
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(1, "notification must see the committed source");
            if (failNotification) throw new InvalidOperationException("Injected post-commit notification failure.");
        }
        projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(_ => new ValueTask(Notify()));
        var repository = new IntrinsicTimeStrategyWorkflowStateRepository(Substitute.For<IEventSourceActorStateFactory>(), eventSource,
            Substitute.For<IActorService>(), projector, NullLogger<IntrinsicTimeStrategyWorkflowStateRepository>.Instance, databases, fixture.Transactions);
        Func<Task> save = async () => await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, command);
        if (failAfterWrite || failNotification) await save.Should().ThrowAsync<InvalidOperationException>();
        else await save();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(failAfterWrite ? 0 : 1);
        var rows = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync("SELECT count(*) FROM trade.intrinsic_time_strategy_workflow WHERE workflowid=$1;", [workflow.Value], token));
        rows.Should().Be(failAfterWrite ? 0L : 1L);
        if (!failAfterWrite)
        {
            var settings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings()
                .Add("TradeDbConnection", fixture.Connection, "System.Data.Postgres");
            var readFactory = Substitute.For<IDbContextFactory>();
            var read = new TradeDbContext(settings, readFactory,
                Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(),
                NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance);
            readFactory.TradeDb.Returns(read);
            var history = await read.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus.Running,
                DateOnly.FromDateTime(now), DateOnly.FromDateTime(now), 1000);
            Assert.Contains(history, row => row.WorkflowId == workflow);
        }
        state.PersistedStreamVersion.Should().Be(failAfterWrite ? 0 : 1);
        state.Events.Count.Should().Be(failAfterWrite ? 1 : 0);
        if (failAfterWrite) await projector.DidNotReceiveWithAnyArgs().DomainEventsProjectionAsync(default!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_and_all_workflow_and_risk_rows_are_atomic_and_rebuildable(bool failAfterWrites)
    {
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], token));
        var now = DateTime.UtcNow;
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(new("native-workflow-" + Guid.NewGuid().ToString("N"), DateOnly.FromDateTime(now), TimeFrameType.Monthly));
        var commandId = Guid.NewGuid();
        var composition = new OrderCompositionResult { ResultId = Guid.NewGuid(), WorkflowId = workflow, EntityId = entity,
            Candidate = new() { PortfolioId = Random.Shared.Next(20_000_000, 25_000_000), FundId = 1 }, EvaluatedAtUtc = now, ProducedAtUtc = now };
        var state = new IntrinsicTimeStrategyWorkflowView { EntityId = entity, WorkflowId = workflow, WorkflowRevision = 1,
            Status = WorkflowStrategyMachineStatus.Started, CurrentStage = StrategyWorkflowStage.RiskManagement,
            WorkflowDefinitionVersion = 1, StartedAtUtc = now, UpdatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(2),
            OrderComposition = new() { Result = StrategyStageResultEnvelope.CreateComposition(composition) } };
        var source = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), CommandId = commandId, EntityId = entity,
            WorkflowId = workflow, WorkflowRevision = 1, ReceivedOn = now, UpdatedAtUtc = now, State = state };
        var stream = "native-workflow-atomic-" + source.Id;
        async Task Save() => await fixture.Transactions.ExecuteOnceAsync(async (db, token) =>
        {
            await db.AppendAsync(new EnlistedEventAppend(stream, commandId, source, 0), token);
            await TradeDbContextExtensions.WorkflowProjectAsync(db, source, token);
            if (failAfterWrites) throw new InvalidOperationException("Injected after all workflow projections before commit.");
            return true;
        });
        if (failAfterWrites) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>();
        else await Save();
        (await fixture.EventCountAsync(stream)).Should().Be(failAfterWrites ? 0 : 1);
        string[] workflowTables = ["intrinsic_time_strategy_workflow", "intrinsic_time_strategy_workflow_active",
            "intrinsic_time_strategy_workflow_by_entity", "intrinsic_time_strategy_workflow_by_status_day",
            "intrinsic_time_strategy_workflow_timeline", "risk_management_invocation"];
        foreach (var table in workflowTables)
        {
            var key = table == "risk_management_invocation" ? "workflow_id" : "workflowId";
            var count = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync("SELECT count(*) FROM trade." + table + " WHERE " + key + "=$1;", [workflow.Value], token));
            count.Should().Be(failAfterWrites ? 0L : 1L, table);
        }
        var attempts = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync("SELECT count(*) FROM trade.intrinsic_time_strategy_workflow_start_attempt WHERE requestedWorkflowId=$1;", [workflow.Value], token));
        attempts.Should().Be(failAfterWrites ? 0L : 1L);
        var risk = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync("SELECT count(*) FROM trade.risk_management_history WHERE invocation_id=$1;", [commandId], token));
        risk.Should().Be(failAfterWrites ? 0L : 1L);
        if (failAfterWrites) return;
        var persisted = await fixture.Transactions.ExecuteOnceAsync(async (db, token) =>
        {
            var rows = await db.QueryAsync("SELECT n.eventtypename,l.eventversion,l.eventpayload FROM event_log l JOIN event_name_id n USING(eventnameid) WHERE l.eventversion=$1;", [source.EventId],
                reader => new EventStreamReadModel { EventTypeName = reader.GetString(0), EventVersion = reader.GetInt64(1), EventData = reader.GetFieldValue<byte[]>(2) }.ToDomainEvent(), token);
            return (WorkflowStrategyStateUpdatedEvent)rows[0];
        });
        await fixture.Transactions.ExecuteOnceAsync(async (db, token) =>
        {
            foreach (var table in workflowTables)
            {
                var key = table == "risk_management_invocation" ? "workflow_id" : "workflowId";
                await db.ExecuteAsync("DELETE FROM trade." + table + " WHERE " + key + "=$1;", [workflow.Value], token);
            }
            await db.ExecuteAsync("DELETE FROM trade.intrinsic_time_strategy_workflow_start_attempt WHERE requestedWorkflowId=$1;", [workflow.Value], token);
            await db.ExecuteAsync("DELETE FROM trade.risk_management_history WHERE invocation_id=$1;", [commandId], token);
            await TradeDbContextExtensions.WorkflowProjectAsync(db, persisted, token);
            await TradeDbContextExtensions.WorkflowProjectAsync(db, persisted, token);
            return true;
        });
        var restored = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync("SELECT statePayload FROM trade.intrinsic_time_strategy_workflow WHERE workflowId=$1;", [workflow.Value], token));
        MessagePack.MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>((byte[])restored!).Should().BeEquivalentTo(state);
    }
}
