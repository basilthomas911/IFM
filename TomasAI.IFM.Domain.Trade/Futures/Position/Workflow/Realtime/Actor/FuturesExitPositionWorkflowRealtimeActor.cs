using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;

public sealed class FuturesExitPositionWorkflowRealtimeActor(
    IRealtimeActorContext<FuturesExitPositionWorkflowRealtimeActor> actorContext)
    : BaseEventActor<FuturesExitPositionWorkflowRealtimeActor>(actorContext, Typed(actorContext).Logger)
{
    public const string ActorName = "FuturesExitPositionWorkflowRealtime";
    readonly IFuturesExitPositionWorkflowRealtimeContext services = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [ExitPositionWorkflowStartedEvent.Verb] = static message =>
                message.AsEvent<ExitPositionWorkflowStartedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ExitPositionWorkflowStartedEvent,
        IFuturesExitPositionWorkflowRealtimeContext, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<ExitPositionWorkflowStartedEvent,
            IFuturesExitPositionWorkflowRealtimeContext, ValueTask>>
        {
            [typeof(ExitPositionWorkflowStartedEvent)] = static (started, context) =>
                started.ExecuteAsync(context)
        }.ToFrozenDictionary();

    protected override IEvent ParseMessage(IEventActorContext<FuturesExitPositionWorkflowRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, ParseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<FuturesExitPositionWorkflowRealtimeActor> context,
        IEvent domainEvent) => ResolveMappedEventHandler(domainEvent, ReceiveMap)(
            (ExitPositionWorkflowStartedEvent)domainEvent, services);
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<FuturesExitPositionWorkflowRealtimeActor> context, ActorThreadId threadId,
        IEvent domainEvent, Exception exception)
    {
        ExitWorkflowLogging.Failed(services.Logger, exception, "Futures", domainEvent.AggregateId);
        return ValueTask.CompletedTask;
    }
    static IFuturesExitPositionWorkflowRealtimeContext Typed(
        IRealtimeActorContext<FuturesExitPositionWorkflowRealtimeActor> context) =>
        context as IFuturesExitPositionWorkflowRealtimeContext ??
        throw new ArgumentException("Typed Futures exit-workflow realtime context required.");
}
