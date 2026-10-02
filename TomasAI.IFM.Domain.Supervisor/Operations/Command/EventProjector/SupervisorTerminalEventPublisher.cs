using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.EventProjector;

/// <summary>Maps committed internal outcomes to operation-specific public terminal events.</summary>
internal static class SupervisorTerminalEventPublisher
{
    /// <summary>Publishes exactly one matching Complete or Fail event for a committed outcome.</summary>
    internal static Task PublishAsync(
        ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
        => outcome.Operation switch
        {
            SupervisorActorOperationKind.Pause => PublishPauseAsync(context, outcome),
            SupervisorActorOperationKind.Drain => PublishDrainAsync(context, outcome),
            SupervisorActorOperationKind.Resume => PublishResumeAsync(context, outcome),
            SupervisorActorOperationKind.Stop => PublishStopAsync(context, outcome),
            SupervisorActorOperationKind.Restart => PublishRestartAsync(context, outcome),
            SupervisorActorOperationKind.Quarantine => PublishQuarantineAsync(context, outcome),
            SupervisorActorOperationKind.Retire => PublishRetireAsync(context, outcome),
            SupervisorActorOperationKind.Recycle => PublishRecycleAsync(context, outcome),
            SupervisorActorOperationKind.AcknowledgeIncident => PublishAcknowledgeIncidentAsync(context, outcome),
            _ => throw new InvalidOperationException("Unsupported Supervisor outcome operation.")
        };

    /// <summary>Publishes the Pause terminal event selected by the committed outcome.</summary>
    static Task PublishPauseAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<PauseSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, PauseSupervisorActorCompleteEvent.Actor, PauseSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<PauseSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, PauseSupervisorActorFailEvent.Actor, PauseSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = PauseSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(PauseSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Drain terminal event selected by the committed outcome.</summary>
    static Task PublishDrainAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<DrainSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, DrainSupervisorActorCompleteEvent.Actor, DrainSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<DrainSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, DrainSupervisorActorFailEvent.Actor, DrainSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = DrainSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(DrainSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Resume terminal event selected by the committed outcome.</summary>
    static Task PublishResumeAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<ResumeSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, ResumeSupervisorActorCompleteEvent.Actor, ResumeSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<ResumeSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, ResumeSupervisorActorFailEvent.Actor, ResumeSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = ResumeSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(ResumeSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Stop terminal event selected by the committed outcome.</summary>
    static Task PublishStopAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<StopSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, StopSupervisorActorCompleteEvent.Actor, StopSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<StopSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, StopSupervisorActorFailEvent.Actor, StopSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = StopSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(StopSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Restart terminal event selected by the committed outcome.</summary>
    static Task PublishRestartAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<RestartSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, RestartSupervisorActorCompleteEvent.Actor, RestartSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<RestartSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, RestartSupervisorActorFailEvent.Actor, RestartSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = RestartSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(RestartSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Quarantine terminal event selected by the committed outcome.</summary>
    static Task PublishQuarantineAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<QuarantineSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, QuarantineSupervisorActorCompleteEvent.Actor, QuarantineSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<QuarantineSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, QuarantineSupervisorActorFailEvent.Actor, QuarantineSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = QuarantineSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(QuarantineSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Retire terminal event selected by the committed outcome.</summary>
    static Task PublishRetireAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<RetireSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, RetireSupervisorActorCompleteEvent.Actor, RetireSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<RetireSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, RetireSupervisorActorFailEvent.Actor, RetireSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = RetireSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(RetireSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the Recycle terminal event selected by the committed outcome.</summary>
    static Task PublishRecycleAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<RecycleSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, RecycleSupervisorActorCompleteEvent.Actor, RecycleSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<RecycleSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, RecycleSupervisorActorFailEvent.Actor, RecycleSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = RecycleSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(RecycleSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

    /// <summary>Publishes the AcknowledgeIncident terminal event selected by the committed outcome.</summary>
    static Task PublishAcknowledgeIncidentAsync(ICommandActorContext context, SupervisorActorOperationRecordedEvent outcome)
    {
        if (outcome.Outcome == SupervisorOperationOutcome.Succeeded)
            return context.SendAsync<AcknowledgeIncidentSupervisorActorCompleteEvent, ActorEntityId>(new()
            {
                Subject = new(ActorType.Event, AcknowledgeIncidentSupervisorActorCompleteEvent.Actor, AcknowledgeIncidentSupervisorActorCompleteEvent.Verb, outcome.EntityId.Format()),
                EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
                CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
                EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
                Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
                Requester = outcome.Requester, Reason = outcome.Reason,
                Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks
            }).AsTask();
        return context.SendAsync<AcknowledgeIncidentSupervisorActorFailEvent, ActorEntityId>(new()
        {
            Subject = new(ActorType.Event, AcknowledgeIncidentSupervisorActorFailEvent.Actor, AcknowledgeIncidentSupervisorActorFailEvent.Verb, outcome.EntityId.Format()),
            EntityId = outcome.EntityId, Id = outcome.Id, EventId = outcome.EventId,
            CommandId = outcome.CommandId, AggregateId = outcome.AggregateId,
            EventSource = outcome.EventSource, ReceivedOn = outcome.ReceivedOn,
            ErrorDate = DateTime.UtcNow, ErrorMessage = outcome.FailureReason,
            ErrorCode = AcknowledgeIncidentSupervisorActorCommand.ErrorId, ErrorType = ErrorType.Command,
            ErrorData = outcome.FailureReason,
            CommandName = nameof(AcknowledgeIncidentSupervisorActorCommand),
            RouteTo = BoundedContextName.SupervisorBoundedContext.ToString(),
            Target = outcome.Target, ExpectedGeneration = outcome.ExpectedGeneration,
            Requester = outcome.Requester, Reason = outcome.Reason,
            Stage = outcome.Stage, TimeoutTicks = outcome.TimeoutTicks,
            Outcome = outcome.Outcome
        }).AsTask();
    }

}
