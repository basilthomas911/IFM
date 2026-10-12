using System.Linq;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
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
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresExitWorkflowRepositoryTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Stage_source_and_complete_projection_commit_or_rollback_together(bool riskStage, bool failAfterWrite)
    {
        var plan = TradePlanStorageTests.Plan(Random.Shared.Next(15_000_000, 20_000_000), 1, -42m, true);
        var id = new ExitPositionWorkflowId(plan.Position.Id, plan.ValueDate, Guid.NewGuid());
        var commandId = Guid.NewGuid();
        var snapshot = new ExitPositionWorkflowProjection { WorkflowId = id, StrategyKind = plan.Position.StrategyKind,
            State = riskStage ? ExitPositionWorkflowState.NoTradeOrders : ExitPositionWorkflowState.OrderComposed,
            StageRevision = riskStage ? 3 : 2, UpdatedAtUtc = DateTime.UtcNow, SourcePlanEventId = Guid.NewGuid(), ExitPlan = plan };
        var source = Substitute.For<IEventSourceActorDbContext>();
        source.ProviderName.Returns("System.Data.Postgres");
        source.ConnectionString.Returns(PostgresTradePlanFixture.Connection);
        var factory = Substitute.For<IDbContextFactory>();
        ITradePlanDbContext target = fixture.Plans;
        if (failAfterWrite)
        {
            target = Substitute.For<ITradePlanDbContext>();
            target.ProviderName.Returns("System.Data.Postgres");
            target.ConnectionString.Returns(PostgresTradePlanFixture.Connection);
            target.ProjectExitWorkflowAsync(Arg.Any<IEnlistedPostgresTransaction>(), Arg.Any<ExitPositionWorkflowProjection>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    await fixture.Plans.ProjectExitWorkflowAsync(call.ArgAt<IEnlistedPostgresTransaction>(0), call.ArgAt<ExitPositionWorkflowProjection>(1), call.ArgAt<CancellationToken>(2));
                    throw new InvalidOperationException("Injected failure after workflow rows were written.");
                });
        }
        factory.TradePlanDb.Returns(target);
        Func<Task> save;
        string stream;
        if (riskStage)
        {
            var request = new EvaluatePositionExitRiskCommand { CommandId = commandId, EntityId = id,
                Subject = new(ActorType.Function, "PostgresExitRiskTest", EvaluatePositionExitRiskCommand.Verb, id.Format()) };
            var state = new PositionExitRiskFunctionState().Prepare(request);
            state.TryComplete(new() { Id = Guid.NewGuid(), CommandId = commandId, EntityId = id, Subject = request.Subject, Projection = snapshot }, request).Should().BeTrue();
            var repository = new PositionExitRiskFunctionStateRepository(Substitute.For<IEventSourceActorStateFactory>(), source,
                Substitute.For<IActorService>(), NullLogger<PositionExitRiskFunctionStateRepository>.Instance, factory, fixture.Transactions);
            save = async () => await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), state, request);
            stream = request.StreamId;
        }
        else
        {
            var request = new ComposeExitOrderCommand { CommandId = commandId, EntityId = id,
                Subject = new(ActorType.Function, "PostgresExitCompositionTest", ComposeExitOrderCommand.Verb, id.Format()) };
            var state = new ExitOrderCompositionFunctionState().Prepare(request);
            state.TryComplete(new() { Id = Guid.NewGuid(), CommandId = commandId, EntityId = id, Subject = request.Subject, Projection = snapshot }, request).Should().BeTrue();
            var repository = new ExitOrderCompositionFunctionStateRepository(Substitute.For<IEventSourceActorStateFactory>(), source,
                Substitute.For<IActorService>(), NullLogger<ExitOrderCompositionFunctionStateRepository>.Instance, factory, fixture.Transactions);
            save = async () => await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), state, request);
            stream = request.StreamId;
        }
        if (failAfterWrite)
        {
            await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected failure*");
            (await fixture.EventCountAsync(stream)).Should().Be(0);
            (await fixture.Plans.GetCurrentExitWorkflowAsync(plan.Position.Id, plan.ValueDate)).Should().BeNull();
        }
        else
        {
            await save();
            (await fixture.EventCountAsync(stream)).Should().Be(1);
            (await fixture.Plans.GetCurrentExitWorkflowAsync(plan.Position.Id, plan.ValueDate)).Should().BeEquivalentTo(snapshot);
        }
    }
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
        var targetConnection = new NpgsqlConnectionStringBuilder(PostgresTradePlanFixture.Connection) { Database = name }.ConnectionString;
        await using var admin = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(PostgresTradePlanFixture.Connection);
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
