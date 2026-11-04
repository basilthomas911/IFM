using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class ParameterStateSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Set_snapshot_preserves_all_versions_and_request_receipts()
    {
        var owner = Guid.NewGuid();
        var values = new RegimeDiscoveryParameterModel().CreateTypedDraft(owner).WithIdentity(owner, 1);
        var reference = new ParameterVersionRef(owner, 1, values.ComponentCode, ParameterValueCodec.Hash(values));
        var projector = Substitute.For<IEventProjector<ParameterSetCommandActor>>();
        var repository = new ParameterSetStateRepository(Factory<ParameterSetCommandState>(), fixture.ActorEventDb,
            Substitute.For<IActorService>(), projector, NullLogger<ParameterSetStateRepository>.Instance);

        await Verify<ParameterSetCommandState, ParameterSetStateSnapshottedEvent>(repository, owner, Create,
            (full, restored) =>
            {
                Assert.Equal(129, restored.CatalogRevision);
                Assert.Equal(2, restored.Versions.Count);
                Assert.Equal(129, restored.Receipts.Count);
                Assert.Equal(129, restored.Audit.Count);
                Assert.Equivalent(full.Versions, restored.Versions);
                Assert.Equivalent(full.Receipts, restored.Receipts);
                Assert.Equivalent(full.Operations, restored.Operations);
                Assert.Equivalent(full.Audit, restored.Audit);
            });
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            events => events.Count == 1 && events[0] is ParameterSetDraftSavedEvent));

        IEvent Create(int revision, Guid operation)
        {
            var version = revision == 1 ? 1 : 2;
            var payload = values.WithIdentity(owner, version);
            var row = new ParameterSetVersion(reference with { Version = version, PayloadSha256 = ParameterValueCodec.Hash(payload) },
                $"Draft {revision}", "", values.SchemaVersion, ParameterVersionStatus.Draft, "", DateTime.UnixEpoch,
                "test", CatalogRevision: revision) { Parameters = payload };
            var audit = new ParameterAuditEntry(operation, owner, revision, "Save", "test", DateTime.UnixEpoch, "", "");
            return revision == 1
                ? new ParameterSetCreatedEvent { Id = Guid.NewGuid(), CommandId = operation, EntityId = new(owner),
                    Revision = revision, Version = row, RequestHash = $"request-{revision}", Audit = audit }
                : new ParameterSetDraftSavedEvent { Id = Guid.NewGuid(), CommandId = operation, EntityId = new(owner),
                    Revision = revision, Version = row, RequestHash = $"request-{revision}", Audit = audit };
        }
    }

    [Fact]
    public async Task Assignment_snapshot_preserves_the_current_revision_and_all_duplicate_evidence()
    {
        var owner = Guid.NewGuid();
        var reference = new ParameterVersionRef(Guid.NewGuid(), 1, RegimeDiscoveryParameterModel.ComponentCode, "hash");
        var scope = WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id, TimeFrameType.Daily);
        var projector = Substitute.For<IEventProjector<ParameterAssignmentCommandActor>>();
        var repository = new ParameterAssignmentStateRepository(Factory<ParameterAssignmentCommandState>(), fixture.ActorEventDb,
            Substitute.For<IActorService>(), projector, NullLogger<ParameterAssignmentStateRepository>.Instance);

        await Verify<ParameterAssignmentCommandState, ParameterAssignmentStateSnapshottedEvent>(repository, owner, Create,
            (full, restored) =>
            {
                Assert.Equal(129, restored.Revision);
                Assert.True(restored.Assignment!.Enabled);
                Assert.Equal(129, restored.Operations.Count);
                Assert.Equal(129, restored.Audit.Count);
                Assert.Equal(full.Assignment, restored.Assignment);
                Assert.Equivalent(full.Operations, restored.Operations);
                Assert.Equivalent(full.Audit, restored.Audit);
            });
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            events => events.Count == 1 && events[0] is ParameterAssignmentChangedEvent));

        IEvent Create(int revision, Guid operation)
        {
            var assignment = new ParameterAssignmentRevision(owner, scope, revision, reference, revision % 2 == 1,
                ParameterApplicationPolicy.NextStartup, DateTime.UnixEpoch, "test");
            var audit = new ParameterAuditEntry(operation, owner, revision, "Assign", "test", DateTime.UnixEpoch, "", "");
            ParameterAssignmentChangedEvent fact = revision == 1 ? new ParameterAssignmentCreatedEvent() : new();
            return fact with { Id = Guid.NewGuid(), CommandId = operation, EntityId = new(owner),
                Revision = revision, Assignment = assignment, RequestHash = $"request-{revision}", Audit = audit };
        }
    }

    private async Task Verify<TState, TSnapshot>(IEventSourceActorStateRepository<TState> repository,
        Guid owner, Func<int, Guid, IEvent> create, Action<TState, TState> assertState)
        where TState : BaseEventSourceActorState<TState>, new()
        where TSnapshot : IEvent
    {
        var subject = new ActorSubject(ActorType.Command, "ParameterSnapshotTest", "Write", owner.ToString("N"));
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(subject);
        command.StreamId.Returns(subject.StreamId);
        var operation = Guid.NewGuid();
        command.CommandId.Returns(operation);
        command.EventSource.Returns("ParameterSnapshotTest");
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var databases = Substitute.For<IDbContextFactory>();
        var configuration = Substitute.For<IConfigurationDbContext>();
        var database = fixture.ActorEventDb;
        configuration.ProviderName.Returns("System.Data.Postgres");
        configuration.ConnectionString.Returns(database.ConnectionString);
        var transactions = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, database.ConnectionString, "System.Data.Postgres"));
        container.Resolve<IPostgresEventTransaction>().Returns(transactions);
        databases.ConfigurationDb.Returns(configuration);
        container.Resolve<IDbContextFactory>().Returns(databases);
        context.Container.Returns(container);

        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(
            Enumerable.Range(1, 127).Select(i => create(i, Guid.NewGuid()))), 0, CancellationToken.None);
        var pending = await repository.LoadStateAsync(command);
        Assert.True(pending.Update(create(128, operation), command));
        await repository.SaveStateAsync(context, pending, command);
        Assert.Equal(129, pending.CommittedStreamVersion);
        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([create(129, Guid.NewGuid())]),
            129, CancellationToken.None);

        var full = new List<double>();
        var snapshot = new List<double>();
        for (var sample = 0; sample < 20; sample++)
        {
            TState baseline;
            TState restored;
            if (sample % 2 == 0)
            {
                baseline = await ReplayFull();
                restored = await Restore();
            }
            else
            {
                restored = await Restore();
                baseline = await ReplayFull();
            }

            Assert.Equal(130, restored.CommittedStreamVersion);
            Assert.Empty(restored.Events);
            assertState(baseline, restored);
        }

        var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        var rows = new List<long>();
        await database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, [typeof(TSnapshot)], page => rows.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, 129, 130 }, rows);
        full.Sort();
        snapshot.Sort();
        output.WriteLine($"{typeof(TState).Name}: 130 -> 3 rows, median " +
            $"{(full[9] + full[10]) / 2:F2} -> {(snapshot[9] + snapshot[10]) / 2:F2} ms, " +
            $"p95 {full[18]:F2} -> {snapshot[18]:F2} ms.");

        async Task<TState> ReplayFull()
        {
            var started = Stopwatch.GetTimestamp();
            var id = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var state = new TState { Id = subject.ThreadId };
            await database.MapReduceActorEventStreamFromCreationAsync<TState>(id, state.ReplayEvents);
            full.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return state;
        }

        async Task<TState> Restore()
        {
            var started = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            snapshot.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return state;
        }
    }

    private static IEventSourceActorStateFactory Factory<TState>() where TState : BaseEventSourceActorState<TState>, new()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        return factory;
    }
}
