using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.EventProjector;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

#pragma warning disable NS1004

public sealed class DatabaseBackupProjectorTests
{
    [Fact]
    public void Projector_registers_every_authoritative_domain_event_once_with_durable_receipts()
    {
        var projector = CreateProjector(Substitute.For<ISystemAdminDbContext>());
        var expected = typeof(DatabaseBackupRequestedDomainEvent).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && typeof(DatabaseBackupEventContract).IsAssignableFrom(type)
                && type.Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) == true);

        projector.ProjectedEventTypes.Should().BeEquivalentTo(expected);
        projector.ProjectionDescriptors.Should().OnlyHaveUniqueItems(descriptor => descriptor.SourceEventType);
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor =>
            descriptor.IdempotencyStrategy == EventProjectionIdempotencyStrategy.TargetReceipt);
    }

    [Fact]
    public async Task Rebuild_clears_only_projection_storage_and_replays_in_event_revision_order()
    {
        var db = Substitute.For<ISystemAdminDbContext>();
        var observed = new List<long>();
        db.ApplyDatabaseBackupEventAsync(
                nameof(DatabaseBackupEventProjector),
                Arg.Any<DatabaseBackupEventContract>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                observed.Add(call.ArgAt<DatabaseBackupEventContract>(1).EventId);
                return ValueTask.FromResult(EventProjectionApplyOutcome.Applied);
            });
        db.GetDatabaseBackupProjectionCheckpointAsync(
                nameof(DatabaseBackupEventProjector), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DatabaseBackupProjectionCheckpoint?>(
                new(nameof(DatabaseBackupEventProjector), 3, 3, DateTimeOffset.UtcNow)));
        var rebuilder = new DatabaseBackupProjectionRebuilder(db);

        var result = await rebuilder.RebuildAsync([Event(3), Event(1), Event(2)]);

        observed.Should().Equal(1, 2, 3);
        result.Should().Be(new DatabaseBackupProjectionRebuildResult(3, 0, 0, 3));
        await db.Received(1).ClearDatabaseBackupProjectionsAsync(
            nameof(DatabaseBackupEventProjector), Arg.Any<CancellationToken>());
    }

    static DatabaseBackupEventProjector CreateProjector(ISystemAdminDbContext db) => new(
        db,
        Substitute.For<IDurableReplayQueue>(),
        Substitute.For<IEventSourceActorDbContext>(),
        Substitute.For<IBlackboardService>(),
        Substitute.For<ILogger<DatabaseBackupEventProjector>>());

    static DatabaseBackupRequestedDomainEvent Event(long eventId)
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        return new DatabaseBackupRequestedDomainEvent
        {
            Id = Guid.NewGuid(), EventId = eventId, CommandId = Guid.NewGuid(), EntityId = operationId,
            AggregateId = operationId.Format(), EventSource = "DatabaseBackupCommandActor", ReceivedOn = DateTime.UtcNow,
            Source = new DatabaseSourceEnvelope
            {
                SourceEventId = Guid.NewGuid(), OperationId = operationId, Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new DatabaseProtectionSetId("core"), PolicyRevision = 1,
                OperationKind = DatabaseRecoveryOperationKind.Backup, Phase = DatabaseRecoveryPhase.Requested,
                CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
            }
        };
    }
}
