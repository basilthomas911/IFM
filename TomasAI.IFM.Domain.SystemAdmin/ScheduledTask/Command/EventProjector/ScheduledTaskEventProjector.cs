using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
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
/// <summary>Projects committed administrative source events into revision-fenced read models; PostgreSQL rows already committed with the source.</summary>
public sealed class ScheduledTaskEventProjector(IScheduledTaskProjectionWriter writer, IDurableReplayQueue queue,
    IEventSourceActorDbContext eventSource, IBlackboardService blackboard, ILogger<ScheduledTaskEventProjector> logger,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<ScheduledTaskCommandActor>(queue, eventSource, blackboard, logger, options)
{
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        new EventProjectionDescriptor(typeof(ScheduledTaskCreatedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskCreatedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskCreatedEvent)domainEvent;
                return new ScheduledTaskCreatedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskCreatedCompleteEvent.Actor, ScheduledTaskCreatedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskCreatedEvent)domainEvent;
                return new ScheduledTaskCreatedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskCreatedFailEvent.Actor, ScheduledTaskCreatedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskCreatedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskScheduleChangedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskScheduleChangedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskScheduleChangedEvent)domainEvent;
                return new ScheduledTaskScheduleChangedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskScheduleChangedCompleteEvent.Actor, ScheduledTaskScheduleChangedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskScheduleChangedEvent)domainEvent;
                return new ScheduledTaskScheduleChangedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskScheduleChangedFailEvent.Actor, ScheduledTaskScheduleChangedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskScheduleChangedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskEnabledEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskEnabledEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskEnabledEvent)domainEvent;
                return new ScheduledTaskEnabledCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskEnabledCompleteEvent.Actor, ScheduledTaskEnabledCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskEnabledEvent)domainEvent;
                return new ScheduledTaskEnabledFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskEnabledFailEvent.Actor, ScheduledTaskEnabledFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskEnabledEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskDisabledEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskDisabledEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskDisabledEvent)domainEvent;
                return new ScheduledTaskDisabledCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskDisabledCompleteEvent.Actor, ScheduledTaskDisabledCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskDisabledEvent)domainEvent;
                return new ScheduledTaskDisabledFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskDisabledFailEvent.Actor, ScheduledTaskDisabledFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskDisabledEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRemovedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRemovedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRemovedEvent)domainEvent;
                return new ScheduledTaskRemovedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskRemovedCompleteEvent.Actor, ScheduledTaskRemovedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRemovedEvent)domainEvent;
                return new ScheduledTaskRemovedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskRemovedFailEvent.Actor, ScheduledTaskRemovedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRemovedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskInstallationRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskInstallationRecordedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskInstallationRecordedEvent)domainEvent;
                return new ScheduledTaskInstallationRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskInstallationRecordedCompleteEvent.Actor, ScheduledTaskInstallationRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskInstallationRecordedEvent)domainEvent;
                return new ScheduledTaskInstallationRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskInstallationRecordedFailEvent.Actor, ScheduledTaskInstallationRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskInstallationRecordedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskInstallationFailureRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskInstallationFailureRecordedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskInstallationFailureRecordedEvent)domainEvent;
                return new ScheduledTaskInstallationFailureRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskInstallationFailureRecordedCompleteEvent.Actor, ScheduledTaskInstallationFailureRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskInstallationFailureRecordedEvent)domainEvent;
                return new ScheduledTaskInstallationFailureRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskInstallationFailureRecordedFailEvent.Actor, ScheduledTaskInstallationFailureRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskInstallationFailureRecordedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunAdmittedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunAdmittedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunAdmittedEvent)domainEvent;
                return new ScheduledTaskRunAdmittedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskRunAdmittedCompleteEvent.Actor, ScheduledTaskRunAdmittedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunAdmittedEvent)domainEvent;
                return new ScheduledTaskRunAdmittedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskRunAdmittedFailEvent.Actor, ScheduledTaskRunAdmittedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunAdmittedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunReleasedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunReleasedEvent)domainEvent).ScheduledTaskDefinition!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunReleasedEvent)domainEvent;
                return new ScheduledTaskRunReleasedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskRunReleasedCompleteEvent.Actor, ScheduledTaskRunReleasedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunReleasedEvent)domainEvent;
                return new ScheduledTaskRunReleasedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskDefinition = source.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskRunReleasedFailEvent.Actor, ScheduledTaskRunReleasedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunReleasedEvent)source).ScheduledTaskDefinition ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false))
    ];
    /// <inheritdoc />
}
