using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;

/// <summary>Publishes only committed opening state; dispatch can always be resumed from its durable source marker.</summary>
public sealed class StrategyExecutionHandoffEventProjector : ConventionalEventProjector<StrategyExecutionHandoffCommandActor>
{
    readonly IReadOnlyCollection<EventProjectionDescriptor> descriptors;

    public StrategyExecutionHandoffEventProjector(ICommandActorContext<StrategyExecutionHandoffCommandActor> context,
        EventProjectorReliabilityOptions? options = null)
        : base(Typed(context).DurableReplayQueue, Typed(context).DbEventSource, Typed(context).Logger, options)
    {
        descriptors = [new EventProjectionDescriptor(typeof(StrategyExecutionHandoffStateSnapshottedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (source, execution) =>
            {
                var store = context.Container.Resolve<StrategyExecutionHandoffReadStore>();
                var snapshot = (StrategyExecutionHandoffStateSnapshottedEvent)source;
                await store.ProjectAsync(snapshot.Definition, execution.CancellationToken).ConfigureAwait(false);
                await context.Container.Resolve<StrategyExecutionHandoffProcessor>().StepAsync(snapshot.Definition, execution.CancellationToken).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            }, _ => null, (_, _) => null, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: false)
            .WithReadModelRebuild(async (source, token) => await context.Container.Resolve<StrategyExecutionHandoffReadStore>()
                .ProjectAsync(((StrategyExecutionHandoffStateSnapshottedEvent)source).Definition, token).ConfigureAwait(false))];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(StrategyExecutionHandoffStateSnapshottedEvent)];
    static IStrategyExecutionHandoffCommandContext Typed(ICommandActorContext<StrategyExecutionHandoffCommandActor> context)
        => context as IStrategyExecutionHandoffCommandContext ?? throw new ArgumentException("Typed handoff context required.", nameof(context));
}
