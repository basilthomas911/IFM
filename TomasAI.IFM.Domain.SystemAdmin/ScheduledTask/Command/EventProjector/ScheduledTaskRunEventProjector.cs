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
public sealed class ScheduledTaskRunEventProjector(IScheduledTaskProjectionWriter writer, IDurableReplayQueue queue,
    IEventSourceActorDbContext eventSource, IBlackboardService blackboard, ILogger<ScheduledTaskRunEventProjector> logger,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<ScheduledTaskRunCommandActor>(queue, eventSource, blackboard, logger, options)
{
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        new EventProjectionDescriptor(typeof(ScheduledTaskRunRequestedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunRequestedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunRequestedEvent)domainEvent;
                return new ScheduledTaskRunRequestedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunRequestedCompleteEvent.Actor, ScheduledTaskRunRequestedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunRequestedEvent)domainEvent;
                return new ScheduledTaskRunRequestedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunRequestedFailEvent.Actor, ScheduledTaskRunRequestedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunRequestedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunAdmissionRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunAdmissionRecordedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunAdmissionRecordedEvent)domainEvent;
                return new ScheduledTaskRunAdmissionRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunAdmissionRecordedCompleteEvent.Actor, ScheduledTaskRunAdmissionRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunAdmissionRecordedEvent)domainEvent;
                return new ScheduledTaskRunAdmissionRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunAdmissionRecordedFailEvent.Actor, ScheduledTaskRunAdmissionRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunAdmissionRecordedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunStartedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunStartedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunStartedEvent)domainEvent;
                return new ScheduledTaskRunStartedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunStartedCompleteEvent.Actor, ScheduledTaskRunStartedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunStartedEvent)domainEvent;
                return new ScheduledTaskRunStartedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunStartedFailEvent.Actor, ScheduledTaskRunStartedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunStartedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)), new EventProjectionDescriptor(typeof(ScheduledTaskRunStageRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunStageRecordedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunStageRecordedEvent)domainEvent;
                return new ScheduledTaskRunStageRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunStageRecordedCompleteEvent.Actor, ScheduledTaskRunStageRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunStageRecordedEvent)domainEvent;
                return new ScheduledTaskRunStageRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunStageRecordedFailEvent.Actor, ScheduledTaskRunStageRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunStageRecordedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunCompletedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunCompletedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunCompletedEvent)domainEvent;
                return new ScheduledTaskRunCompletedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunCompletedCompleteEvent.Actor, ScheduledTaskRunCompletedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunCompletedEvent)domainEvent;
                return new ScheduledTaskRunCompletedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunCompletedFailEvent.Actor, ScheduledTaskRunCompletedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunCompletedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunFailedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunFailedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunFailedEvent)domainEvent;
                return new ScheduledTaskRunFailedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunFailedCompleteEvent.Actor, ScheduledTaskRunFailedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunFailedEvent)domainEvent;
                return new ScheduledTaskRunFailedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunFailedFailEvent.Actor, ScheduledTaskRunFailedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunFailedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false)),        new EventProjectionDescriptor(typeof(ScheduledTaskRunUncertainRecordedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                if (writer is not IScheduledTaskTransactionalProjectionWriter { ProviderName: "System.Data.Postgres" })
                    await writer.ProjectAsync(((ScheduledTaskRunUncertainRecordedEvent)domainEvent).ScheduledTaskRun!, execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            }, domainEvent =>
            {
                var source = (ScheduledTaskRunUncertainRecordedEvent)domainEvent;
                return new ScheduledTaskRunUncertainRecordedCompleteEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunUncertainRecordedCompleteEvent.Actor, ScheduledTaskRunUncertainRecordedCompleteEvent.Verb, source.EntityId.Format()) };
            }, (domainEvent, exception) =>
            {
                var source = (ScheduledTaskRunUncertainRecordedEvent)domainEvent;
                return new ScheduledTaskRunUncertainRecordedFailEvent { Id = source.Id, EventId = source.EventId, CommandId = source.CommandId, EntityId = source.EntityId, AggregateId = source.AggregateId, EventSource = source.EventSource, OperationCommandId = source.OperationCommandId, ScheduledTaskRun = source.ScheduledTaskRun, Subject = new(ActorType.Event, ScheduledTaskRunUncertainRecordedFailEvent.Actor, ScheduledTaskRunUncertainRecordedFailEvent.Verb, source.EntityId.Format()), ErrorMessage = exception.Message, ErrorType = TomasAI.IFM.Shared.EventSourcing.ErrorType.Storage };
            }, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: true).WithReadModelRebuild(async (source, token) => await (writer.ProjectAsync(((ScheduledTaskRunUncertainRecordedEvent)source).ScheduledTaskRun ?? throw new InvalidDataException("Missing committed scheduled-task snapshot."), token)).ConfigureAwait(false))
    ];
    /// <inheritdoc />
}
