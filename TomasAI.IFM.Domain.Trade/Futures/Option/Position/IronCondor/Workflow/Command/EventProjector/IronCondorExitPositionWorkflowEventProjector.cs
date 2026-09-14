using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.EventProjector;

public sealed class IronCondorExitPositionWorkflowEventProjector
    : ConventionalEventProjector<IronCondorExitPositionWorkflowCommandActor>
{
    readonly IIronCondorExitPositionWorkflowCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public IronCondorExitPositionWorkflowEventProjector(
        ICommandActorContext<IronCondorExitPositionWorkflowCommandActor> actorContext,
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
                IronCondorExitPositionWorkflowRealtimeActor.ActorName,
                ExitPositionWorkflowStartedEvent.Verb, started.EntityId.Format())
        }).AsTask();

    static IIronCondorExitPositionWorkflowCommandContext Typed(
        ICommandActorContext<IronCondorExitPositionWorkflowCommandActor> context) =>
        context as IIronCondorExitPositionWorkflowCommandContext ??
        throw new ArgumentException("Typed Iron Condor exit-workflow context required.");
}
