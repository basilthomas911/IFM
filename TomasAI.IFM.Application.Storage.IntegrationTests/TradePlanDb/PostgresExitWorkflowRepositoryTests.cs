using TomasAI.IFM.Application.EventSourcing.Projections.TradePlan;
using System.Linq;
using System.IO;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using Xunit.Abstractions;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresExitWorkflowRepositoryTests(PostgresTradePlanFixture fixture, ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Owner_source_and_complete_projection_commit_or_rollback_together(bool riskStage, bool failAfterWrite)
    {
        var (start, started) = MakeStart();
        await using var source = CreateSource();
        var repo = Repository(source, fixture.Plans);
        await SeedStart(start, started);
        var composed = await SeedComposition(started);
        var state = await repo.LoadStateAsync(start);
        var compose = new RecordExitOrderCompositionCommand
        {
            CommandId = Guid.NewGuid(), Subject = Subject(started, RecordExitOrderCompositionCommand.Verb), EntityId = started.EntityId,
            ExpectedRevision = 1, PredecessorEventId = started.Id, Receipt = composed
        };
        compose.Execute(state).Success.Should().BeTrue();
        ICommand command = compose;
        if (riskStage)
        {
            await repo.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, compose);
            state = await repo.LoadStateAsync(start);
            var risk = await SeedRisk(started, composed);
            var record = new RecordExitRiskDecisionCommand
            {
                CommandId = Guid.NewGuid(), Subject = Subject(started, RecordExitRiskDecisionCommand.Verb), EntityId = started.EntityId,
                ExpectedRevision = 2, PredecessorEventId = state.CurrentSourceEventId, Receipt = risk
            };
            record.Execute(state).Success.Should().BeTrue();
            command = record;
        }

        var snapshot = state.Snapshot!.Projection;
        var pendingSource = state.Events.Single();
        var target = failAfterWrite ? FailingProjection() : fixture.Plans;
        var writer = Repository(source, target);
        Func<Task> save = async () => await writer.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, command);
        var countBefore = riskStage ? 2 : 1;
        if (failAfterWrite)
        {
            await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected failure*");
            (await fixture.EventCountAsync(start.StreamId)).Should().Be(countBefore);
            var current = await fixture.Plans.GetCurrentExitWorkflowAsync(started.EntityId.Position, started.EntityId.ValueDate);
            current!.StageRevision.Should().Be(countBefore);
            return;
        }
        await save();
        if (!riskStage)
        {
            var marker = await source.GetEventProjectorExecutionStateAsync(
                pendingSource.EventId, ExitPositionWorkflowActorNames.DurableProjection(started.StrategyKind).ProjectorName);
            marker.Should().NotBeNull();
            marker!.Stage.Should().Be(EventProjectorStageType.ApplyProjection);
        }
        (await fixture.EventCountAsync(start.StreamId)).Should().Be(countBefore + 1);
        (await fixture.Plans.GetCurrentExitWorkflowAsync(started.EntityId.Position, started.EntityId.ValueDate)).Should().BeEquivalentTo(snapshot);
        var restored = await Repository(source, fixture.Plans).LoadStateAsync(start);
        restored.Snapshot.Should().BeEquivalentTo(state.Snapshot);
        restored.CommittedStreamVersion.Should().Be(countBefore + 1);
        restored.Events.Should().BeEmpty();

        if (riskStage)
            await MeasureReplayAsync(source, start, repo, restored);
    }

    [Fact]
    public async Task Composition_with_different_payload_cannot_reuse_an_existing_receipt_identity()
    {
        var (start, started) = MakeStart();
        await using var source = CreateSource();
        var repo = Repository(source, fixture.Plans);
        await SeedStart(start, started);
        var receipt = await SeedComposition(started);
        var forged = new RecordExitOrderCompositionCommand
        {
            CommandId = Guid.NewGuid(), Subject = Subject(started, RecordExitOrderCompositionCommand.Verb), EntityId = started.EntityId,
            ExpectedRevision = 1, PredecessorEventId = started.Id,
            Receipt = receipt with { Composition = receipt.Composition with { CompositionHash = "forged-composition" } }
        };
        var state = await repo.LoadStateAsync(forged);
        forged.Execute(state).Success.Should().BeTrue();
        var append = () => repo.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, forged).AsTask();
        await append.Should().ThrowAsync<InvalidDataException>().WithMessage("Missing or different committed evidence*");
        (await fixture.EventCountAsync(start.StreamId)).Should().Be(1);
        (await repo.LoadStateAsync(start)).Snapshot!.Projection.State.Should().Be(ExitPositionWorkflowState.Started);
    }

    [Fact]
    public async Task Legacy_reconciliation_uses_only_committed_receipts_and_does_not_redispatch_historical_orders()
    {
        var (start, started) = MakeStart(lifecycleVersion: 0);
        await using var source = CreateSource();
        var repo = Repository(source, fixture.Plans);
        await SeedStart(start, started);
        var producer = Substitute.For<IActorProducer>();
        (await ExitWorkflowLegacyReconciliation.ReconcileAsync(started.EntityId, started.StrategyKind, fixture.Transactions, producer))
            .Should().BeFalse();
        producer.ReceivedCalls().Should().BeEmpty();

        var composition = await SeedComposition(started);
        producer.RequestAsync<RecordExitOrderCompositionCommand, ExitPositionWorkflowId, GuidResult>(
            Arg.Any<ActorSubject>(), Arg.Any<RecordExitOrderCompositionCommand>(), started.EntityId, Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<ServiceResult<GuidResult>>(ApplyCompositionAsync(call.Arg<RecordExitOrderCompositionCommand>())));

        (await ExitWorkflowLegacyReconciliation.ReconcileAsync(started.EntityId, started.StrategyKind, fixture.Transactions, producer))
            .Should().BeTrue();
        var composed = await repo.LoadStateAsync(start);
        composed.Snapshot!.Projection.State.Should().Be(ExitPositionWorkflowState.OrderComposed);
        (await ExitWorkflowLegacyReconciliation.ReconcileAsync(started.EntityId, started.StrategyKind, fixture.Transactions, producer))
            .Should().BeFalse("risk may advance only when a committed historical risk receipt exists");
        producer.ReceivedCalls().Should().ContainSingle();

        var decision = new PortfolioCloseRiskDecision
        {
            ExecuteTradeOrder = true, TradeOrder = new() { Id = new(1, 2, Random.Shared.Next(31_000_000, 40_000_000)) },
            PortfolioCompletedEventId = Guid.NewGuid(), FinancialRevision = 1
        };
        await SeedRisk(started, composition, decision);
        producer.RequestAsync<RecordExitRiskDecisionCommand, ExitPositionWorkflowId, GuidResult>(
            Arg.Any<ActorSubject>(), Arg.Any<RecordExitRiskDecisionCommand>(), started.EntityId, Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<ServiceResult<GuidResult>>(ApplyRiskAsync(call.Arg<RecordExitRiskDecisionCommand>())));
        (await ExitWorkflowLegacyReconciliation.ReconcileAsync(started.EntityId, started.StrategyKind, fixture.Transactions, producer))
            .Should().BeTrue();
        (await ExitWorkflowLegacyReconciliation.ReconcileAsync(started.EntityId, started.StrategyKind, fixture.Transactions, producer))
            .Should().BeFalse("order completion requires a committed execution binding");

        var order = decision.TradeOrder!;
        var bindingSubject = new ActorSubject(ActorType.Command, TradeOrderActorNames.Command, BindTradeOrderExecutionCommand.Verb, order.Id.Format());
        var dispatchSeed = HandoffId($"exit-order|{decision.PortfolioCompletedEventId:N}|{order.Id.Format()}");
        await AppendReceipt(bindingSubject.StreamId, new TradeOrderChangedEvent
        {
            Id = Guid.NewGuid(), EntityId = order.Id, Subject = bindingSubject, ReceivedOn = started.ReceivedOn.AddSeconds(3),
            CommandId = HandoffId($"exit-order-bind|{dispatchSeed:N}|{order.Id.Format()}"),
            ExecutionAttemptId = HandoffId($"exit-order-execution|{dispatchSeed:N}|{order.Id.Format()}"),
            ExecutionChannel = ExecutionChannel.Broker
        });
        producer.RequestAsync<CompleteExitPositionWorkflowCommand, ExitPositionWorkflowId, GuidResult>(
            Arg.Any<ActorSubject>(), Arg.Any<CompleteExitPositionWorkflowCommand>(), started.EntityId, Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<ServiceResult<GuidResult>>(ApplyCompletionAsync(call.Arg<CompleteExitPositionWorkflowCommand>())));
        (await ExitWorkflowLegacyReconciliation.ReconcileAsync(started.EntityId, started.StrategyKind, fixture.Transactions, producer))
            .Should().BeTrue();
        (await repo.LoadStateAsync(start)).Snapshot!.Projection.State.Should().Be(ExitPositionWorkflowState.Completed);
        producer.ReceivedCalls().Should().HaveCount(3);

        async Task<ServiceResult<GuidResult>> ApplyCompositionAsync(RecordExitOrderCompositionCommand command)
        {
            var state = await repo.LoadStateAsync(command);
            var result = command.Execute(state);
            result.Success.Should().BeTrue(result.ErrorMessage);
            await repo.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, command);
            return result;
        }

        async Task<ServiceResult<GuidResult>> ApplyRiskAsync(RecordExitRiskDecisionCommand command)
        {
            var state = await repo.LoadStateAsync(command);
            var result = command.Execute(state);
            result.Success.Should().BeTrue(result.ErrorMessage);
            await repo.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, command);
            return result;
        }

        async Task<ServiceResult<GuidResult>> ApplyCompletionAsync(CompleteExitPositionWorkflowCommand command)
        {
            var state = await repo.LoadStateAsync(command);
            var result = command.Execute(state);
            result.Success.Should().BeTrue(result.ErrorMessage);
            await repo.SaveStateAsync(Substitute.For<ICommandActorContext>(), state, command);
            return result;
        }
    }

    ITradePlanDbContext FailingProjection()
    {
        var target = Substitute.For<ITradePlanDbContext>();
        target.ProviderName.Returns("System.Data.Postgres");
        target.ConnectionString.Returns(fixture.Connection);
        target.ProjectExitWorkflowAsync(Arg.Any<IEnlistedPostgresTransaction>(), Arg.Any<ExitPositionWorkflowProjection>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await fixture.Plans.ProjectExitWorkflowAsync(call.ArgAt<IEnlistedPostgresTransaction>(0), call.ArgAt<ExitPositionWorkflowProjection>(1), call.ArgAt<CancellationToken>(2));
                throw new InvalidOperationException("Injected failure after workflow rows were written.");
            });
        return target;
    }

    (StartIronCondorExitPositionWorkflowCommand Command, ExitPositionWorkflowStartedEvent Started) MakeStart(ushort lifecycleVersion = 1)
    {
        var plan = TradePlanStorageTests.Plan(Random.Shared.Next(15_000_000, 20_000_000), 1, -42m, true) with
        { State = TradePlanState.ExitRequired, Action = TradePlanAction.ExitAtMarket, RequiresExit = true };
        var id = new ExitPositionWorkflowId(plan.Position.Id, plan.ValueDate, Guid.NewGuid());
        var started = new ExitPositionWorkflowStartedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id, StrategyKind = plan.Position.StrategyKind,
            LifecycleVersion = lifecycleVersion, SourcePlanEventId = Guid.NewGuid(), ExitPlan = plan, ReceivedOn = DateTime.UtcNow
        };
        return (new() { EntityId = id, CommandId = started.CommandId, Subject = Subject(started, StartIronCondorExitPositionWorkflowCommand.Verb) }, started);
    }

    async Task SeedStart(ICommand command, ExitPositionWorkflowStartedEvent started) => await fixture.Transactions.ExecuteOnceAsync(async (db, token) =>
    {
        await db.AppendAsync(command.StreamId, command.CommandId, started, 0, token);
        await fixture.Plans.ProjectExitWorkflowAsync(db, ExitWorkflowSnapshotFacts.From(started), token);
        return true;
    });

    async Task<ExitOrderCompositionCompletedEvent> SeedComposition(ExitPositionWorkflowStartedEvent started)
    {
        var subject = new ActorSubject(ActorType.Function, ExitPositionWorkflowActorNames.IronCondorOrderComposer,
            ComposeExitOrderCommand.Verb, started.EntityId.Format());
        var receipt = new ExitOrderCompositionCompletedEvent
        {
            Id = Guid.NewGuid(), EntityId = started.EntityId, Subject = subject, ReceivedOn = started.ReceivedOn.AddSeconds(1),
            CommandId = TradePlanContractIdentity.DeterministicId($"{started.EntityId.Format()}|{ComposeExitOrderCommand.Verb}"),
            RequestFingerprint = FinancialCanonicalHash.Compute(new { started.EntityId, started.StrategyKind, started.SourcePlanEventId, Plan = started.ExitPlan.ContentHash }),
            Composition = new() { WorkflowId = started.EntityId, StrategyKind = started.StrategyKind, Position = started.ExitPlan.Position,
                ExitAction = started.ExitPlan.Action, CompositionHash = "composition-evidence" }
        };
        await AppendReceipt(subject.StreamId, receipt);
        return receipt;
    }

    async Task<PositionExitRiskCompletedEvent> SeedRisk(ExitPositionWorkflowStartedEvent started, ExitOrderCompositionCompletedEvent composed,
        PortfolioCloseRiskDecision? decision = null)
    {
        var subject = new ActorSubject(ActorType.Function, ExitPositionWorkflowActorNames.IronCondorRiskManager,
            EvaluatePositionExitRiskCommand.Verb, started.EntityId.Format());
        var receipt = new PositionExitRiskCompletedEvent
        {
            Id = Guid.NewGuid(), EntityId = started.EntityId, Subject = subject, ReceivedOn = composed.ReceivedOn.AddSeconds(1),
            CommandId = TradePlanContractIdentity.DeterministicId($"{started.EntityId.Format()}|{EvaluatePositionExitRiskCommand.Verb}"),
            RequestFingerprint = FinancialCanonicalHash.Compute(new { started.EntityId, started.StrategyKind,
                CompositionEventId = composed.Id, composed.Composition.CompositionHash }),
            Decision = decision ?? new() { ExecuteTradeOrder = false, ReasonCode = "NoClose" }
        };
        await AppendReceipt(subject.StreamId, receipt);
        return receipt;
    }

    async Task AppendReceipt(string stream, IEvent receipt) =>
        await fixture.Transactions.ExecuteOnceAsync(async (db, token) =>
        {
            await db.AppendAsync(stream, receipt.CommandId, receipt, 0, token);
            return true;
        });

    IronCondorExitPositionWorkflowStateRepository Repository(EventSourceActorDbContext source, ITradePlanDbContext target)
    {
        var factory = Substitute.For<IDbContextFactory>();
        factory.TradePlanDb.Returns(target);
        var states = Substitute.For<IEventSourceActorStateFactory>();
        states.CreateState<IronCondorExitPositionWorkflowCommandState>().Returns(_ => new IronCondorExitPositionWorkflowCommandState());
        return new(states, source, Substitute.For<IActorService>(), Substitute.For<IEventProjector<IronCondorExitPositionWorkflowCommandActor>>(),
            NullLogger<IronCondorExitPositionWorkflowStateRepository>.Instance, factory, fixture.Transactions);
    }

    EventSourceActorDbContext CreateSource()
    {
        EventSourceActorDbContext source = null!;
        var factory = new DbContextFactory(new DbContextResolver(_ => source));
        return source = new(fixture.Settings, factory,
            new BlackboardService(Substitute.For<IRedisCache>(), new SystemTextJsonSerializer()), NullLogger<DbProvider>.Instance);
    }

    async Task MeasureReplayAsync(EventSourceActorDbContext source, ICommand command, IronCondorExitPositionWorkflowStateRepository repo,
        IronCondorExitPositionWorkflowCommandState expected)
    {
        var before = new System.Collections.Generic.List<double>();
        var after = new System.Collections.Generic.List<double>();
        const int samples = 25;
        for (var sample = 0; sample < samples; sample++)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            var stream = await source.GetEventStreamIdAsync(command.StreamId);
            var replayed = new IronCondorExitPositionWorkflowCommandState();
            await source.MapReduceActorEventStreamFromCreationAsync<IronCondorExitPositionWorkflowCommandState>(
                stream, replayed.ReplayEvents, CancellationToken.None);
            before.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            var latest = await repo.LoadStateAsync(command);
            after.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            latest.Snapshot.Should().BeEquivalentTo(replayed.Snapshot);
            latest.Snapshot.Should().BeEquivalentTo(expected.Snapshot);
        }
        before.Sort();
        after.Sort();
        const int medianIndex = samples / 2;
        var p95Index = (int)Math.Ceiling(samples * 0.95) - 1;
        output.WriteLine($"Exit workflow (stream resolution included, {samples} samples): full replay 3 rows median {before[medianIndex]:F3} ms and p95 {before[p95Index]:F3} ms. Snapshot 2 rows median {after[medianIndex]:F3} ms and p95 {after[p95Index]:F3} ms.");
    }

    static ActorSubject Subject(ExitPositionWorkflowStartedEvent started, string verb) =>
        new(ActorType.Command, ExitPositionWorkflowActorNames.Owner(started.StrategyKind), verb, started.EntityId.Format());

    static Guid HandoffId(string input) =>
        new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)).AsSpan(0, 16));

    [Fact]
    public async Task Verified_baseline_rebuilds_in_fresh_database_using_only_persisted_source_bytes()
    {
        var plan = TradePlanStorageTests.Plan(Random.Shared.Next(25_000_000, 30_000_000), 1, -42m, true);
        var seed = new ExitPositionWorkflowProjection { WorkflowId = new(plan.Position.Id, plan.ValueDate, Guid.NewGuid()),
            StrategyKind = plan.Position.StrategyKind, State = ExitPositionWorkflowState.Started, StageRevision = 1,
            UpdatedAtUtc = DateTime.UtcNow, SourcePlanEventId = Guid.NewGuid(), ExitPlan = plan };
        await fixture.Plans.ProjectExitWorkflowAsync(seed);
        var baseline = new ExitWorkflowProjectionBaseline(fixture.Transactions, fixture.Plans);
        await Assert.ThrowsAsync<ArgumentException>(() => baseline.CaptureAsync(false));
        var captured = await baseline.CaptureAsync(true);
        var source = (await baseline.ReadLatestAsync())!;
        source.Snapshots.Should().BeEquivalentTo(captured.Snapshots);
        source.Snapshots.Should().Contain(x => x.WorkflowId == seed.WorkflowId);
        source.EventId.Should().BeGreaterThan(source.SourceCut);
        var name = "ifm_exit_verify_" + Guid.NewGuid().ToString("N");
        var targetConnection = new NpgsqlConnectionStringBuilder(fixture.Connection) { Database = name }.ConnectionString;
        await using var admin = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE DATABASE " + name + ";", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, targetConnection, "System.Data.Postgres")
                .Add(TradePlanDbContext.TradePlanDbConnection, targetConnection, "System.Data.Postgres");
            await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
            await new TradePlanSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
            var target = new TradePlanDbContext(settings, NullLogger<DbProvider>.Instance);
            var rebuild = new ExitWorkflowProjectionBaseline(new PostgresEventTransaction(settings), target);
            await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => rebuild.MaterializeAsync(source with { PayloadSha256 = "invalid" }));
            await rebuild.MaterializeAsync(source);
            await rebuild.MaterializeAsync(source);
            foreach (var snapshot in source.Snapshots.GroupBy(x => (x.WorkflowId.Position, x.WorkflowId.ValueDate)))
            {
                var latest = snapshot.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.WorkflowId.ExitDecisionId).ThenByDescending(x => x.StageRevision).First();
                (await target.GetCurrentExitWorkflowAsync(latest.WorkflowId.Position, latest.WorkflowId.ValueDate)).Should().BeEquivalentTo(latest);
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand("DROP DATABASE " + name + " WITH (FORCE);", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

}
