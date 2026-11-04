using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests.ParameterSets;

[Collection(ReferenceIntegrationInfrastructureCollection.Name)]
public sealed class ParameterOwnershipRuntimeTests(ReferenceIntegrationInfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Explicit_legacy_import_uses_prepared_evidence_without_changing_the_source_configuration()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        var api = new ParameterSetsApi(infrastructure.ActorProducer);
        var legacy = TomasAI.IFM.Domain.Reference.ParameterSets.Model.RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
        await infrastructure.ConfigurationDb.InsertRegimeDiscoveryDraftAsync(legacy, "Legacy import evidence", "integration", token);
        var before = Assert.Single(await infrastructure.ConfigurationDb.GetLegacyParameterVersionsAsync(legacy.ParameterSetId, legacy.Version, cancellationToken: token));
        var preview = await api.PreviewLegacyMigrationAsync(legacy.ParameterSetId, legacy.Version, token);
        Assert.True(preview.Success, preview.ErrorMessage);
        var imported = await api.CreateAsync(preview.Value!, token);
        Assert.True(imported.Success, imported.ErrorMessage);
        var repeated = await api.CreateAsync(preview.Value!, token);
        Assert.True(repeated.Success, repeated.ErrorMessage);
        var own = await api.StateAsync(preview.Value!.EntityId.SetId, token);
        Assert.True(own.Success, own.ErrorMessage);
        Assert.Equal(1, own.Value!.Revision);
        Assert.Equal(before.Reference, Assert.Single(own.Value.Versions).LegacySource);
        var after = Assert.Single(await infrastructure.ConfigurationDb.GetLegacyParameterVersionsAsync(legacy.ParameterSetId, legacy.Version, cancellationToken: token));
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task A_new_assignment_invalidates_prepared_retirement_before_any_source_event_is_committed()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        var api = new ParameterSetsApi(infrastructure.ActorProducer);
        var raw = new RawParameters(infrastructure.ActorProducer);
        var id = Guid.NewGuid();
        var draft = await api.PreviewValuesAsync(id, ParameterSchemaRegistry.RegimeComponent, token: token);
        Assert.True(draft.Success, draft.ErrorMessage);
        Assert.True((await api.CreateAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), Name = "Retirement fence",
            SchemaVersion = ParameterSchemaRegistry.CurrentRegimeSchemaVersion, Parameters = draft.Value! }, token)).Success);
        var published = await api.PublishAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), Version = 1, ExpectedRevision = 1 }, token);
        Assert.True(published.Success, published.ErrorMessage);
        var selected = (await api.StateAsync(id, token)).Value!.Versions.Single();
        var retirement = new RetireParameterVersionCommand { CommandId = Guid.NewGuid(), EntityId = new(id), Version = 1, ExpectedRevision = 2,
            Subject = new(ActorType.Command, RetireParameterVersionCommand.Actor, RetireParameterVersionCommand.Verb, id.ToString("N")) };
        var prepared = await raw.Prepare(new(Retirement: retirement), token);
        Assert.True(prepared.Success, prepared.ErrorMessage);
        Assert.False(prepared.Value!.Retirement!.Evidence!.HasActiveAssignments);
        var scope = WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id, TimeFrameType.Daily);
        var identity = new ParameterAssignmentEntityId(ParameterAssignmentPolicyModel.AssignmentId(scope));
        var before = await api.AssignmentAsync(scope, token);
        Assert.True(before.Success, before.ErrorMessage);
        var assigned = await api.AssignAsync(new() { CommandId = Guid.NewGuid(), EntityId = identity, Scope = scope,
            Reference = selected.Reference, ExpectedRevision = before.Value!.Revision }, token);
        Assert.True(assigned.Success, assigned.ErrorMessage);
        try
        {
            var stale = await raw.Retire(prepared.Value.Retirement, token);
            Assert.False(stale.Success);
            Assert.Contains("PARAM.EVIDENCE_STALE", stale.ErrorMessage);
            Assert.False(await infrastructure.ActorEventSourceDb.HasEventForCommandAsync(retirement.CommandId));
            var unchanged = await api.StateAsync(id, token);
            Assert.Equal(2, unchanged.Value!.Revision);
            Assert.Equal(ParameterVersionStatus.Published, unchanged.Value.Versions.Single().Status);
            var current = await api.RetireAsync(retirement, token);
            Assert.False(current.Success);
            Assert.Contains("PARAM.VERSION_IN_USE", current.ErrorMessage);
        }
        finally
        {
            var current = await api.AssignmentAsync(scope, token);
            var disabled = await api.DisableAssignmentAsync(new() { CommandId = Guid.NewGuid(), EntityId = identity, Scope = scope,
                Reference = selected.Reference, ExpectedRevision = current.Value!.Revision }, token);
            Assert.True(disabled.Success, disabled.ErrorMessage);
        }
        var retried = await api.RetireAsync(retirement, token);
        Assert.True(retried.Success, retried.ErrorMessage);
        Assert.Equal(3, (await api.StateAsync(id, token)).Value!.Revision);
    }

    [Fact]
    public async Task Startup_runs_have_independent_creation_snapshots_and_legacy_query_routes_remain_readable()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        var api = new ParameterSetsApi(infrastructure.ActorProducer);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        foreach (var id in new[] { first, second })
        {
            var started = await api.ApplyStartupAsync(new() { CommandId = id, RunId = id }, token);
            Assert.True(started.Success, started.ErrorMessage);
        }
        try
        {
            var legacy = await new RawParameters(infrastructure.ActorProducer).LegacyRun(second, token);
            Assert.True(legacy.Success, legacy.ErrorMessage);
            Assert.Equal(second, legacy.Value!.RunId);
            var released = await api.ReleaseStartupAsync(new() { CommandId = Guid.NewGuid(), RunId = first }, token);
            Assert.True(released.Success, released.ErrorMessage);
            var firstState = await Repository().LoadStateAsync(Address(first), token);
            var secondState = await Repository().LoadStateAsync(Address(second), token);
            Assert.True(firstState.Released);
            Assert.Equal(2, firstState.CommittedStreamVersion);
            Assert.False(secondState.Released);
            Assert.Equal(1, secondState.CommittedStreamVersion);
            Assert.Equal(second, Assert.Single(secondState.ActiveRuns).Key);
            var firstStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(Address(first).StreamId, token);
            var secondStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(Address(second).StreamId, token);
            Assert.NotEqual(firstStream, secondStream);
            var creation = Assert.Single(await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<ParameterStartupCreatedEvent>(secondStream));
            Assert.Contains(nameof(ParameterStartupCreatedEvent), creation.EventTypeName);
            var repeated = await api.ApplyStartupAsync(new() { CommandId = first, RunId = first }, token);
            Assert.True(repeated.Success, repeated.ErrorMessage);
            Assert.True((await Repository().LoadStateAsync(Address(first), token)).Released);
        }
        finally
        {
            foreach (var id in new[] { first, second })
                Assert.True((await api.ReleaseStartupAsync(new() { CommandId = Guid.NewGuid(), RunId = id }, token)).Success);
        }
    }

    [Fact]
    public async Task Interleaved_historical_registry_is_checkpointed_into_one_run_stream_without_reactivation()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        var api = new ParameterSetsApi(infrastructure.ActorProducer);
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var run = new ParameterStartupRun(id, [], [], new(id, "legacy", "legacy", [], null), DateTime.UtcNow, "integration");
        var other = run with { RunId = otherId, Plan = run.Plan with { StartupRunId = otherId } };
        var registry = Address(ParameterStartupEntityId.Registry.Id);
        var report = new ParameterSignalStartupReport(id, "legacy", new DateOnly(2026, 11, 3), "ES", DateTime.UtcNow, []);
        var lease = await infrastructure.ConfigurationDb.AcquireParameterWriteLeaseAsync(token);
        await using (lease.ConfigureAwait(false))
        {
            var stream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(registry.StreamId, token);
            var version = await infrastructure.ActorEventSourceDb.GetCommittedStreamVersionAsync(stream, token);
            ParameterStartupChangedEvent[] facts = [
                new() { EntityId = ParameterStartupEntityId.Registry, CommandId = id, RunId = id, Revision = version + 1, Run = run },
                new() { EntityId = ParameterStartupEntityId.Registry, CommandId = otherId, RunId = otherId, Revision = version + 2, Run = other },
                new() { EntityId = ParameterStartupEntityId.Registry, CommandId = Guid.NewGuid(), RunId = id, Revision = version + 3, Report = report }
            ];
            await infrastructure.EventTransactions.ExecuteAsync(async (transaction, cancellation) =>
            {
                foreach (var fact in facts)
                {
                    await transaction.AppendAsync(registry.StreamId, fact.CommandId, fact, version++, cancellation);
                    await infrastructure.ConfigurationDb.ProjectParameterStartupAsync(transaction, fact, cancellation);
                }
                await infrastructure.ConfigurationDb.AdvanceParameterRevisionFenceAsync(transaction, cancellation);
                return true;
            }, token);
        }
        try
        {
            var inspected = await api.StartupRunAsync(id, token);
            Assert.True(inspected.Success, inspected.ErrorMessage);
            Assert.Equal(id, inspected.Value!.RunId);
            var released = await api.ReleaseStartupAsync(new() { CommandId = Guid.NewGuid(), RunId = id }, token);
            Assert.True(released.Success, released.ErrorMessage);
            var restored = await Repository().LoadStateAsync(Address(id), token);
            Assert.True(restored.Released);
            Assert.Equal(2, restored.Revision);
            Assert.Equal(2, restored.CommittedStreamVersion);
            Assert.Empty(restored.Events);
            Assert.False(restored.Updated);
            var ownStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(Address(id).StreamId, token);
            var replay = await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<ParameterStartupCreatedEvent>(ownStream);
            Assert.Equal(2, replay.Count);
            Assert.Contains(nameof(ParameterStartupCreatedEvent), replay.First().EventTypeName);
            var repeat = await api.ApplyStartupAsync(new() { CommandId = id, RunId = id }, token);
            Assert.True(repeat.Success, repeat.ErrorMessage);
            Assert.True((await Repository().LoadStateAsync(Address(id), token)).Released);
            Assert.True((await api.StartupRunAsync(otherId, token)).Success);
        }
        finally
        {
            foreach (var runId in new[] { id, otherId })
                Assert.True((await api.ReleaseStartupAsync(new() { CommandId = Guid.NewGuid(), RunId = runId }, token)).Success);
        }
    }

    ParameterStartupStateRepository Repository()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ParameterStartupCommandState>().Returns(_ => new ParameterStartupCommandState());
        factory.CreateState<ParameterStartupRegistryState>().Returns(_ => new ParameterStartupRegistryState());
        return new(factory, infrastructure.ActorEventSourceDb, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ParameterStartupCommandActor>>(), NullLogger<ParameterStartupStateRepository>.Instance);
    }

    static ApplySignalStartupPlanCommand Address(Guid id) => new() { EntityId = new(id), RunId = id,
        Subject = new(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, id.ToString("N")) };

    sealed class RawParameters(IActorProducer producer) : NatsClientApi(producer)
    {
        public async Task<ServiceResult<ParameterCommandPreparation>> Prepare(ParameterCommandPreparation preparation, CancellationToken token)
        {
            var query = new PrepareParameterCommandQuery { Preparation = preparation,
                Subject = new(ActorType.Query, PrepareParameterCommandQuery.Actor, PrepareParameterCommandQuery.Verb, ActorEntityId.Default.Format()) };
            return await RequestAsync<PrepareParameterCommandQuery, ParameterCommandPreparation>(query.Subject, query, token);
        }

        public async Task<ServiceResult<GuidResult>> Retire(RetireParameterVersionCommand command, CancellationToken token)
            => await RequestCommandResultAsync<RetireParameterVersionCommand, ParameterSetEntityId, GuidResult>(command, command.EntityId, token);

        public async Task<ServiceResult<ParameterStartupRun>> LegacyRun(Guid id, CancellationToken token)
        {
            var query = new GetParameterStartupRunQuery { RunId = id,
                Subject = new(ActorType.Query, "ParameterSetQuery", GetParameterStartupRunQuery.Verb, ActorEntityId.Default.Format()) };
            return await RequestAsync<GetParameterStartupRunQuery, ParameterStartupRun>(query.Subject, query, token);
        }
    }
}
