using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
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
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.EventProjector;
/// <summary>Projects committed administrative source events into revision-fenced ScyllaDB read models.</summary>
public sealed class ScheduledTaskCatalogEventProjector(IScheduledTaskProjectionWriter writer, IDurableReplayQueue queue,
    IEventSourceActorDbContext eventSource, IBlackboardService blackboard, ILogger<ScheduledTaskCatalogEventProjector> logger,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<ScheduledTaskCatalogCommandActor>(queue, eventSource, blackboard, logger, options)
{
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        new(typeof(ScheduledTaskProjectRegisteredEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                await writer.ProjectAsync(((ScheduledTaskProjectRegisteredEvent)domainEvent).ScheduledTaskCatalog!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskProjectRegisteredEvent)domainEvent;
                return new ScheduledTaskProjectRegisteredCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskProjectRegisteredCompleteEvent.Actor, ScheduledTaskProjectRegisteredCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskProjectRegisteredEvent)domainEvent;
                return new ScheduledTaskProjectRegisteredFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskProjectRegisteredFailEvent.Actor, ScheduledTaskProjectRegisteredFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true),        new(typeof(ScheduledTaskHostCapabilityRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                await writer.ProjectAsync(((ScheduledTaskHostCapabilityRecordedEvent)domainEvent).ScheduledTaskCatalog!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskHostCapabilityRecordedEvent)domainEvent;
                return new ScheduledTaskHostCapabilityRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskHostCapabilityRecordedCompleteEvent.Actor, ScheduledTaskHostCapabilityRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskHostCapabilityRecordedEvent)domainEvent;
                return new ScheduledTaskHostCapabilityRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskCatalog = source.ScheduledTaskCatalog, Subject = new(ActorType.Event, ScheduledTaskHostCapabilityRecordedFailEvent.Actor, ScheduledTaskHostCapabilityRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true)
    ];
    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes => ProjectionDescriptors.Select(d => d.SourceEventType).ToArray();
}
