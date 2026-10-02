using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.EventProjector;

/// <summary>Publishes typed terminal events only after the Supervisor outcome commits.</summary>
public sealed class SupervisorEventProjector(
    ICommandActorContext<SupervisorCommandActor> actorContext,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext eventSource,
    IBlackboardService blackboard,
    ILogger<SupervisorEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<SupervisorCommandActor>(
        durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<SupervisorActorOperationRecordedEvent,
            TomasAI.IFM.Shared.EventModelActor.ActorEntityId>(
            outcome => SupervisorTerminalEventPublisher.PublishAsync(actorContext, outcome),
            useDurableReplay: false)
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
