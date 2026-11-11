using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Validation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.EventProjector;
/// <summary>Projects committed administrative source events into revision-fenced read models; PostgreSQL rows already committed with the source.</summary>
public sealed class ScheduledTaskCatalogEventProjector(IScheduledTaskProjectionWriter writer, IDurableReplayQueue queue,
    IEventSourceActorDbContext eventSource, ILogger<ScheduledTaskCatalogEventProjector> logger,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<ScheduledTaskCatalogCommandActor>(queue, eventSource, logger, options)
{
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        new EventProjectionDescriptor(typeof(ScheduledTaskCatalogProjectRegisteredEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskCatalogProjectRegisteredEvent)domainEvent).ScheduledTaskCatalog ?? throw new InvalidDataException("Missing committed ScheduledTaskCatalog source payload."), execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskCatalogProjectRegisteredEvent)domainEvent;
                return new ScheduledTaskCatalogProjectRegisteredCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskCatalogProjectRegisteredCompleteEvent.Actor, ScheduledTaskCatalogProjectRegisteredCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskCatalogProjectRegisteredEvent)domainEvent;
                return new ScheduledTaskCatalogProjectRegisteredFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskCatalogProjectRegisteredFailEvent.Actor, ScheduledTaskCatalogProjectRegisteredFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskCatalogProjectRegisteredEvent)source).ScheduledTaskCatalog ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskCatalogHostCapabilityRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskCatalogHostCapabilityRecordedEvent)domainEvent).ScheduledTaskCatalog ?? throw new InvalidDataException("Missing committed ScheduledTaskCatalog source payload."), execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskCatalogHostCapabilityRecordedEvent)domainEvent;
                return new ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent.Actor, ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskCatalogHostCapabilityRecordedEvent)domainEvent;
                return new ScheduledTaskCatalogHostCapabilityRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskCatalogHostCapabilityRecordedFailEvent.Actor, ScheduledTaskCatalogHostCapabilityRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskCatalogHostCapabilityRecordedEvent)source).ScheduledTaskCatalog ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false))
    ];
    /// <inheritdoc />
}
