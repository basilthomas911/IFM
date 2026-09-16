using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime.Actor;

public sealed class VerticalSpreadExitPositionWorkflowRealtimeActor(
    IRealtimeActorContext<VerticalSpreadExitPositionWorkflowRealtimeActor> actorContext)
    : BaseEventActor<VerticalSpreadExitPositionWorkflowRealtimeActor>(actorContext, Typed(actorContext).Logger)
{
    public const string ActorName = "VerticalSpreadExitPositionWorkflowRealtime";
    readonly IVerticalSpreadExitPositionWorkflowRealtimeContext services = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [ExitPositionWorkflowStartedEvent.Verb] = static message =>
                message.AsEvent<ExitPositionWorkflowStartedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ExitPositionWorkflowStartedEvent,
        IVerticalSpreadExitPositionWorkflowRealtimeContext, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ExitPositionWorkflowStartedEvent,
            IVerticalSpreadExitPositionWorkflowRealtimeContext, ValueTask>>
        {
            [typeof(ExitPositionWorkflowStartedEvent)] = static (started, context) =>
                started.ExecuteAsync(context)
        }.ToFrozenDictionary();

    protected override IEvent ParseMessage(IEventActorContext<VerticalSpreadExitPositionWorkflowRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<VerticalSpreadExitPositionWorkflowRealtimeActor> context,
        IEvent domainEvent) => ResolveMappedEventHandler(domainEvent, _receiveMap)(
            (ExitPositionWorkflowStartedEvent)domainEvent, services);
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<VerticalSpreadExitPositionWorkflowRealtimeActor> context, ActorThreadId threadId,
        IEvent domainEvent, Exception exception)
    {
        ExitWorkflowLogging.Failed(services.Logger, exception, "VerticalSpread", domainEvent.AggregateId);
        return ValueTask.CompletedTask;
    }
    static IVerticalSpreadExitPositionWorkflowRealtimeContext Typed(
        IRealtimeActorContext<VerticalSpreadExitPositionWorkflowRealtimeActor> context) =>
        context as IVerticalSpreadExitPositionWorkflowRealtimeContext ??
        throw new ArgumentException("Typed Vertical Spread exit-workflow realtime context required.");
}
