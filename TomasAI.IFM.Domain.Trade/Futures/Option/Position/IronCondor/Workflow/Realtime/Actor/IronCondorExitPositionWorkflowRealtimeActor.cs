using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;

public sealed class IronCondorExitPositionWorkflowRealtimeActor(
    IRealtimeActorContext<IronCondorExitPositionWorkflowRealtimeActor> actorContext)
    : BaseEventActor<IronCondorExitPositionWorkflowRealtimeActor>(actorContext, Typed(actorContext).Logger)
{
    public const string ActorName = "IronCondorExitPositionWorkflowRealtime";
    readonly IIronCondorExitPositionWorkflowRealtimeContext services = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [ExitPositionWorkflowStartedEvent.Verb] = static message =>
                message.AsEvent<ExitPositionWorkflowStartedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ExitPositionWorkflowStartedEvent,
        IIronCondorExitPositionWorkflowRealtimeContext, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ExitPositionWorkflowStartedEvent,
            IIronCondorExitPositionWorkflowRealtimeContext, ValueTask>>
        {
            [typeof(ExitPositionWorkflowStartedEvent)] = static (started, context) =>
                started.ExecuteAsync(context)
        }.ToFrozenDictionary();

    protected override IEvent ParseMessage(IEventActorContext<IronCondorExitPositionWorkflowRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<IronCondorExitPositionWorkflowRealtimeActor> context,
        IEvent domainEvent) => ResolveMappedEventHandler(domainEvent, _receiveMap)(
            (ExitPositionWorkflowStartedEvent)domainEvent, services);
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<IronCondorExitPositionWorkflowRealtimeActor> context, ActorThreadId threadId,
        IEvent domainEvent, Exception exception)
    {
        ExitWorkflowLogging.Failed(services.Logger, exception, "IronCondor", domainEvent.AggregateId);
        return ValueTask.CompletedTask;
    }
    static IIronCondorExitPositionWorkflowRealtimeContext Typed(
        IRealtimeActorContext<IronCondorExitPositionWorkflowRealtimeActor> context) =>
        context as IIronCondorExitPositionWorkflowRealtimeContext ??
        throw new ArgumentException("Typed Iron Condor exit-workflow realtime context required.");
}
