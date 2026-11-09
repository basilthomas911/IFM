using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;

using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Projection;

using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;

public abstract class DatabaseOperationEventProjector<TActor> : BaseEventProjector<TActor> where TActor : ICommandActor<TActor>
{
    static readonly ImmutableArray<Type> EventTypes = typeof(DatabaseBackupRequestedDomainEvent).Assembly
        .GetTypes()
        .Where(static type => !type.IsAbstract
            && typeof(DatabaseBackupEventContract).IsAssignableFrom(type)
            && type.Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) == true)
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToImmutableArray();

    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    protected DatabaseOperationEventProjector(
        ISystemAdminDbContext systemAdminDb,
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext eventSource,
        ILogger logger,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(durableReplayQueue, eventSource, logger, reliabilityOptions)
    {
        _descriptors = [.. EventTypes.Select(type => Describe(type, systemAdminDb))];
    }

    public override string ActorName => typeof(TActor).Name;
    public override string ProjectorName => GetType().Name;
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";
    public override IReadOnlyCollection<Type> ProjectedEventTypes => EventTypes;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    static EventProjectionDescriptor Describe(
        Type eventType,
        ISystemAdminDbContext db,
        bool useDurableReplay = true)
        => new EventProjectionDescriptor(
            eventType,
            EventProjectionIdempotencyStrategy.TargetReceipt,
            async (domainEvent, execution) =>
            {
                if (string.Equals(db.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                    return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
                var outcome = await db.ApplyDatabaseBackupSnapshotAsync(
                    "DatabaseBackupEventProjector",
                    DatabaseBackupProjectionFactory.Create((DatabaseBackupEventContract)domainEvent),
                    execution.CancellationToken).ConfigureAwait(false);
                return new EventProjectionApplyResult(outcome);
            },
            domainEvent => Complete((DatabaseBackupEventContract)domainEvent),
            (domainEvent, exception) => Fail((DatabaseBackupEventContract)domainEvent, exception),
            useDurableReplay: useDurableReplay).WithReadModelRebuild(async (source,token)=>
            {
                await db.ApplyDatabaseBackupSnapshotAsync("DatabaseBackupEventProjector",DatabaseBackupProjectionFactory.Create((DatabaseBackupEventContract)source),token).ConfigureAwait(false);
            });

    static DatabaseBackupProjectionCompletedEvent Complete(DatabaseBackupEventContract source) => new()
    {
        Subject = new ActorSubject(ActorType.Event, "DatabaseBackupProjectionEvent", "ProjectionCompleted", source.EntityId.Format()),
        EntityId = source.EntityId,
        Id = source.Id,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = "DatabaseBackupEventProjector",
        ReceivedOn = default
    };

    static DatabaseBackupProjectionFailedEvent Fail(DatabaseBackupEventContract source, Exception exception) => new()
    {
        Subject = new ActorSubject(ActorType.Event, "DatabaseBackupProjectionEvent", "ProjectionFailed", source.EntityId.Format()),
        EntityId = source.EntityId,
        Id = source.Id,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = "DatabaseBackupEventProjector",

        ErrorDate = DateTime.UtcNow,
        ErrorMessage = exception.Message,
        CommandName = source.EventName
    };
}
