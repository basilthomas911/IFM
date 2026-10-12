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
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresWorkflowSnapshotTests(PostgresTradePlanFixture fixture)
{
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
            Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>(),
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
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaSql.Create, [], token));
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
        eventSource.ConnectionString.Returns(PostgresTradePlanFixture.Connection);
        var target = Substitute.For<ITradeDbContext, IEnlistedTradeDbWriter>();
        target.ProviderName.Returns("System.Data.Postgres");
        target.ConnectionString.Returns(PostgresTradePlanFixture.Connection);
        ((IEnlistedTradeDbWriter)target).ProjectWorkflowAsync(Arg.Any<IEnlistedPostgresTransaction>(), Arg.Any<WorkflowStrategyStateUpdatedEvent>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await PostgresWorkflowSnapshotStore.ProjectAsync(call.ArgAt<IEnlistedPostgresTransaction>(0), call.ArgAt<WorkflowStrategyStateUpdatedEvent>(1), call.ArgAt<CancellationToken>(2));
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
                .Add("TradeDbConnection", PostgresTradePlanFixture.Connection, "System.Data.Postgres");
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
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaSql.Create, [], token));
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
            await db.AppendAsync(stream, commandId, source, 0, token);
            await PostgresWorkflowSnapshotStore.ProjectAsync(db, source, token);
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
            await PostgresWorkflowSnapshotStore.ProjectAsync(db, persisted, token);
            await PostgresWorkflowSnapshotStore.ProjectAsync(db, persisted, token);
            return true;
        });
        var restored = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync("SELECT statePayload FROM trade.intrinsic_time_strategy_workflow WHERE workflowId=$1;", [workflow.Value], token));
        MessagePack.MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>((byte[])restored!).Should().BeEquivalentTo(state);
    }
}
