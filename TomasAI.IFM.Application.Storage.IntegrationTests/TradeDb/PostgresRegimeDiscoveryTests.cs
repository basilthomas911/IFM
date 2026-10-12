using System.Threading;
using System.IO;
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure", "SelfContained")]
public sealed class PostgresRegimeDiscoveryTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Regime_source_audit_and_projection_share_commit_or_rollback(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaSql.Create, [], ct));
        var now = DateTime.UtcNow;
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId("ES.TEST", DateOnly.FromDateTime(now), TimeFrameType.Daily));
        var execution = RegimeDiscoveryExecutionEntityId.Create(entity, workflow);
        var request = new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = Guid.NewGuid(), EntityId = execution, InputWorkflowRevision = 1, RequestedAtUtc = now, ExpiresAtUtc = now.AddMinutes(5),
            Subject = new ActorSubject(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor, ExecuteRegimeDiscoveryPipelineCommand.Verb, execution.Format())
        };
        var signal = Guid.NewGuid();
        var result = new RegimeDiscoveryResult { ResultId = request.CommandId, WorkflowId = workflow, EntityId = entity, SignalSnapshotId = signal, ProducedAtUtc = now, MarketDataAsOfUtc = now };
        var candidate = new RegimeDiscoveryPipelineCompletedEvent
        {
            Id = Guid.NewGuid(), CommandId = request.CommandId, Subject = request.Subject, EntityId = entity, WorkflowId = workflow,
            InputWorkflowRevision = 1, ReceivedOn = now, CompletedAtUtc = now, ExpiresAtUtc = request.ExpiresAtUtc, SignalSnapshotId = signal,
            ParameterPayloadSha256 = new string('A', 64), Result = StrategyStageResultEnvelope.CreateRegime(result)
        };
        var settings = new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection, PostgresTradePlanFixture.Connection, "System.Data.Postgres");
        var factory = new Mock<IDbContextFactory>();
        var target = new TradeDbContext(settings, factory.Object, Mock.Of<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        factory.SetupGet(x => x.TradeDb).Returns(target);
        var source = new Mock<IEventSourceActorDbContext>();
        source.SetupGet(x => x.ProviderName).Returns("System.Data.Postgres");
        source.SetupGet(x => x.ConnectionString).Returns(PostgresTradePlanFixture.Connection);
        var owner = new Mock<IPostgresEventTransaction>();
        owner.Setup(x => x.ExecuteOnceAsync(It.IsAny<Func<EnlistedEventTransaction, CancellationToken, Task<RegimeDiscoveryPipelineCompletedEvent>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<EnlistedEventTransaction, CancellationToken, Task<RegimeDiscoveryPipelineCompletedEvent>> operation, CancellationToken ct) =>
                fixture.Transactions.ExecuteOnceAsync(async (db, token) => { var accepted = await operation(db, token); if (fail) throw new InvalidOperationException("Injected before commit"); return accepted; }, ct));
        var repository = new RegimeDiscoveryFunctionStateRepository(Mock.Of<IEventSourceActorStateFactory>(), source.Object, Mock.Of<IActorService>(),
            NullLogger<RegimeDiscoveryFunctionStateRepository>.Instance, factory.Object, owner.Object);
        repository.UsesAtomicPersistence.Should().BeTrue();
        async Task Save() => await repository.CommitAsync(Mock.Of<IFunctionActorContext>(), request, candidate);
        if (fail) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>(); else await Save();
        (await fixture.EventCountAsync(request.StreamId)).Should().Be(fail ? 0 : 1);
        var projected = await target.GetRegimeDiscoveryAsync(workflow);
        if (fail) projected.Should().BeNull();
        else
        {
            projected!.SourceEventId.Should().Be(candidate.Id);
            await fixture.Transactions.ExecuteOnceAsync(async (db, ct) => { await PostgresRegimeDiscoveryStore.ProjectAsync(db, candidate, ct); return true; });
            await FluentActions.Awaiting(() => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
            {
                await PostgresRegimeDiscoveryStore.ProjectAsync(db, candidate with { CompletedAtUtc = now.AddSeconds(1) }, ct);
                return true;
            })).Should().ThrowAsync<InvalidDataException>();
            (await target.GetRegimeDiscoveryAsync(workflow))!.TerminalAtUtc.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));
        }
        var audit = await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ScalarAsync("SELECT count(*) FROM command_log WHERE commandid=$1;", [request.CommandId], ct));
        Convert.ToInt64(audit).Should().Be(fail ? 0 : 1);
    }
}
