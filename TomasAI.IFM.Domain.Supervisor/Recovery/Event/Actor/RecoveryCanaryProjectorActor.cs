using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event.Actor;

/// <summary>Routes side-effect-free recovery canary events to the Supervisor projection.</summary>
public sealed class RecoveryCanaryProjectorActor(IEventActorContext<RecoveryCanaryProjectorActor> context)
    : BaseEventActor<RecoveryCanaryProjectorActor>(context, Typed(context).Logger)
{
    public const string ActorName = RecoveryCanaryAcceptedEvent.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [RecoveryCanaryAcceptedEvent.Verb] = static message =>
                message.AsEvent<RecoveryCanaryAcceptedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IRecoveryCanaryEventContext, IEvent, ILogger<RecoveryCanaryProjectorActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IRecoveryCanaryEventContext, IEvent,
            ILogger<RecoveryCanaryProjectorActor>, ValueTask>>
        {
            [typeof(RecoveryCanaryAcceptedEvent)] = static (owner, value, logger) =>
                ((RecoveryCanaryAcceptedEvent)value).ExecuteAsync(owner, logger)
        }.ToFrozenDictionary();

    protected override ValueTask OnStartup(IEventActorContext<RecoveryCanaryProjectorActor> context)
    {
        var owner = Typed(context);
        owner.SupervisorRuntime.RegisterProjector(owner.Projection);
        owner.Projection.SetRunning(true);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext<RecoveryCanaryProjectorActor> context)
    {
        Typed(context).Projection.SetRunning(false);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(IEventActorContext<RecoveryCanaryProjectorActor> context,
        IActorMessage message) => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<RecoveryCanaryProjectorActor> context,
        IEvent value) => ResolveMappedEventHandler(value, _receiveMap)(Typed(context), value,
            Typed(context).Logger);

    protected override ValueTask OnExceptionAsync(IEventActorContext<RecoveryCanaryProjectorActor> context,
        ActorThreadId threadId, IEvent value, Exception exception)
    {
        Typed(context).Logger.LogError(exception,
            "Supervisor recovery canary projector failed for {ActorThreadId}.", threadId);
        return ValueTask.CompletedTask;
    }

    static IRecoveryCanaryEventContext Typed(IEventActorContext<RecoveryCanaryProjectorActor> context) =>
        context as IRecoveryCanaryEventContext
        ?? throw new ArgumentException("A recovery canary event context is required.", nameof(context));
}
