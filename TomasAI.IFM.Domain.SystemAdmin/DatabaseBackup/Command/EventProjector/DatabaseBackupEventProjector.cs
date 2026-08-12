using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Projection;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.EventProjector;

public sealed class DatabaseBackupEventProjector : BaseEventProjector<DatabaseBackupCommandActor>
{
    static readonly ImmutableArray<Type> EventTypes = typeof(DatabaseBackupRequestedDomainEvent).Assembly
        .GetTypes()
        .Where(static type => !type.IsAbstract
            && typeof(DatabaseBackupEventContract).IsAssignableFrom(type)
            && type.Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) == true)
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToImmutableArray();

    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    public DatabaseBackupEventProjector(
        ISystemAdminDbContext systemAdminDb,
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext eventSource,
        IBlackboardService blackboard,
        ILogger<DatabaseBackupEventProjector> logger,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
    {
        _descriptors = [.. EventTypes.Select(type => Describe(type, systemAdminDb))];
    }

    public override string ActorName => nameof(DatabaseBackupCommandActor);
    public override string ProjectorName => nameof(DatabaseBackupEventProjector);
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";
    public override IReadOnlyCollection<Type> ProjectedEventTypes => EventTypes;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    static EventProjectionDescriptor Describe(Type eventType, ISystemAdminDbContext db)
        => new(
            eventType,
            EventProjectionIdempotencyStrategy.TargetReceipt,
            async (domainEvent, execution) =>
            {
                var outcome = await db.ApplyDatabaseBackupEventAsync(
                    execution.ProjectorName,
                    (DatabaseBackupEventContract)domainEvent,
                    execution.CancellationToken).ConfigureAwait(false);
                return new EventProjectionApplyResult(outcome);
            },
            domainEvent => Complete((DatabaseBackupEventContract)domainEvent),
            (domainEvent, exception) => Fail((DatabaseBackupEventContract)domainEvent, exception));

    static DatabaseBackupProjectionCompletedEvent Complete(DatabaseBackupEventContract source) => new()
    {
        Subject = new ActorSubject(ActorType.Event, "DatabaseBackupProjectionEvent", "ProjectionCompleted", source.EntityId.Format()),
        EntityId = source.EntityId,
        Id = source.Id,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = nameof(DatabaseBackupEventProjector),
        ReceivedOn = DateTime.UtcNow
    };

    static DatabaseBackupProjectionFailedEvent Fail(DatabaseBackupEventContract source, Exception exception) => new()
    {
        Subject = new ActorSubject(ActorType.Event, "DatabaseBackupProjectionEvent", "ProjectionFailed", source.EntityId.Format()),
        EntityId = source.EntityId,
        Id = source.Id,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = nameof(DatabaseBackupEventProjector),
        ReceivedOn = DateTime.UtcNow,
        ErrorDate = DateTime.UtcNow,
        ErrorMessage = exception.Message,
        CommandName = source.EventName
    };
}
