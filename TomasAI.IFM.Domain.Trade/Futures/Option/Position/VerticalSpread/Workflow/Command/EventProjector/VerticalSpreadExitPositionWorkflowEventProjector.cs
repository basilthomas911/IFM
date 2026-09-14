using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.EventProjector;

public sealed class VerticalSpreadExitPositionWorkflowEventProjector
    : ConventionalEventProjector<VerticalSpreadExitPositionWorkflowCommandActor>
{
    readonly IVerticalSpreadExitPositionWorkflowCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public VerticalSpreadExitPositionWorkflowEventProjector(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> actorContext,
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
                VerticalSpreadExitPositionWorkflowRealtimeActor.ActorName,
                ExitPositionWorkflowStartedEvent.Verb, started.EntityId.Format())
        }).AsTask();

    static IVerticalSpreadExitPositionWorkflowCommandContext Typed(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context) =>
        context as IVerticalSpreadExitPositionWorkflowCommandContext ??
        throw new ArgumentException("Typed Vertical Spread exit-workflow context required.");
}
