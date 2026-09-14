using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.EventProjector;

public sealed class FuturesExitPositionWorkflowEventProjector
    : ConventionalEventProjector<FuturesExitPositionWorkflowCommandActor>
{
    readonly IFuturesExitPositionWorkflowCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public FuturesExitPositionWorkflowEventProjector(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        context = Typed(actorContext);
        descriptors = [DescribeNotification<ExitPositionWorkflowStartedEvent,
            ExitPositionWorkflowId>(ProjectAsync)];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(ExitPositionWorkflowStartedEvent)];

    Task ProjectAsync(ExitPositionWorkflowStartedEvent started) => context.SendAsync<
        ExitPositionWorkflowStartedEvent, ExitPositionWorkflowId>(started with
        {
            Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime,
                FuturesExitPositionWorkflowRealtimeActor.ActorName,
                ExitPositionWorkflowStartedEvent.Verb, started.EntityId.Format())
        }).AsTask();

    static IFuturesExitPositionWorkflowCommandContext Typed(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) =>
        context as IFuturesExitPositionWorkflowCommandContext ??
        throw new ArgumentException("Typed Futures exit-workflow context required.");
}
