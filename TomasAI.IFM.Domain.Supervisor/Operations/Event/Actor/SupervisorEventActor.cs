using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;

/// <summary>Routes Supervisor operational audit events to their dedicated handler.</summary>
public sealed class SupervisorEventActor(IEventActorContext<SupervisorEventActor> context)
    : BaseEventActor<SupervisorEventActor>(context, Typed(context).Logger)
{
    public const string ActorName = PauseSupervisorActorCompleteEvent.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [PauseSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<PauseSupervisorActorCompleteEvent>()!,
            [PauseSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<PauseSupervisorActorFailEvent>()!,
            [DrainSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<DrainSupervisorActorCompleteEvent>()!,
            [DrainSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<DrainSupervisorActorFailEvent>()!,
            [ResumeSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<ResumeSupervisorActorCompleteEvent>()!,
            [ResumeSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<ResumeSupervisorActorFailEvent>()!,
            [StopSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<StopSupervisorActorCompleteEvent>()!,
            [StopSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<StopSupervisorActorFailEvent>()!,
            [RestartSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<RestartSupervisorActorCompleteEvent>()!,
            [RestartSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<RestartSupervisorActorFailEvent>()!,
            [QuarantineSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<QuarantineSupervisorActorCompleteEvent>()!,
            [QuarantineSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<QuarantineSupervisorActorFailEvent>()!,
            [RetireSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<RetireSupervisorActorCompleteEvent>()!,
            [RetireSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<RetireSupervisorActorFailEvent>()!,
            [RecycleSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<RecycleSupervisorActorCompleteEvent>()!,
            [RecycleSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<RecycleSupervisorActorFailEvent>()!,
            [AcknowledgeIncidentSupervisorActorCompleteEvent.Verb] = static message =>
                message.AsEvent<AcknowledgeIncidentSupervisorActorCompleteEvent>()!,
            [AcknowledgeIncidentSupervisorActorFailEvent.Verb] = static message =>
                message.AsEvent<AcknowledgeIncidentSupervisorActorFailEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<ISupervisorEventActorContext, IEvent, ILogger<SupervisorEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISupervisorEventActorContext, IEvent,
            ILogger<SupervisorEventActor>, ValueTask>>
        {
            [typeof(PauseSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((PauseSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(PauseSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((PauseSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(DrainSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((DrainSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(DrainSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((DrainSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(ResumeSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((ResumeSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(ResumeSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((ResumeSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(StopSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((StopSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(StopSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((StopSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(RestartSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((RestartSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(RestartSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((RestartSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(QuarantineSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((QuarantineSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(QuarantineSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((QuarantineSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(RetireSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((RetireSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(RetireSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((RetireSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(RecycleSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((RecycleSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(RecycleSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((RecycleSupervisorActorFailEvent)value).ExecuteAsync(owner, logger),
            [typeof(AcknowledgeIncidentSupervisorActorCompleteEvent)] = static (owner, value, logger) =>
                ((AcknowledgeIncidentSupervisorActorCompleteEvent)value).ExecuteAsync(owner, logger),
            [typeof(AcknowledgeIncidentSupervisorActorFailEvent)] = static (owner, value, logger) =>
                ((AcknowledgeIncidentSupervisorActorFailEvent)value).ExecuteAsync(owner, logger)
        }.ToFrozenDictionary();

    protected override IEvent ParseMessage(IEventActorContext<SupervisorEventActor> context,
        IActorMessage message) => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<SupervisorEventActor> context,
        IEvent value) => ResolveMappedEventHandler(value, _receiveMap)(Typed(context), value,
            Typed(context).Logger);

    protected override ValueTask OnExceptionAsync(IEventActorContext<SupervisorEventActor> context,
        ActorThreadId threadId, IEvent value, Exception exception)
    {
        Typed(context).Logger.LogError(exception, "Supervisor event failed for {ActorThreadId}.", threadId);
        return ValueTask.CompletedTask;
    }

    static ISupervisorEventActorContext Typed(IEventActorContext<SupervisorEventActor> context) =>
        context as ISupervisorEventActorContext
        ?? throw new ArgumentException("A Supervisor event context is required.", nameof(context));
}
