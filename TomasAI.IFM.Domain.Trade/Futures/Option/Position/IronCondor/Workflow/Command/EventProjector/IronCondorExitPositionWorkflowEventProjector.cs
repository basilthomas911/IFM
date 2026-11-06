using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.EventProjector;
public sealed class IronCondorExitPositionWorkflowEventProjector : ConventionalEventProjector<IronCondorExitPositionWorkflowCommandActor>
{
    readonly IReadOnlyCollection<EventProjectionDescriptor> descriptors;
    public IronCondorExitPositionWorkflowEventProjector(ICommandActorContext<IronCondorExitPositionWorkflowCommandActor> actorContext, IActorSupervisor supervisor, IPostgresEventTransaction transactions, EventProjectorReliabilityOptions? options = null) : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource, Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        var context = Typed(actorContext);
        descriptors = ExitWorkflowProjectionDescriptors.Create(context.DbFactory.TradePlanDb, () => supervisor.GetProducer(context.ActorId), transactions);
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => ExitWorkflowSnapshotFacts.CheckpointTypes;

    static IIronCondorExitPositionWorkflowCommandContext Typed(ICommandActorContext<IronCondorExitPositionWorkflowCommandActor> context) => context as IIronCondorExitPositionWorkflowCommandContext ?? throw new ArgumentException("Typed Iron Condor exit-workflow context required.", nameof(context));
}
