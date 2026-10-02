using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.EventProjector;

/// <summary>Publishes the canary proof event only after its source event commits.</summary>
public sealed class RecoveryCanaryEventProjector(
    ICommandActorContext<RecoveryCanaryCommandActor> actorContext,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext eventSource,
    IBlackboardService blackboard,
    ILogger<RecoveryCanaryEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<RecoveryCanaryCommandActor>(
        durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<RecoveryCanaryRecordedEvent, ActorEntityId>(async recorded =>
        {
            var accepted = new RecoveryCanaryAcceptedEvent
            {
                Subject = new(ActorType.Event, RecoveryCanaryAcceptedEvent.Actor,
                    RecoveryCanaryAcceptedEvent.Verb, recorded.CorrelationId.ToString("N")),
                Id = Guid.NewGuid(),
                EntityId = recorded.EntityId,
                CommandId = recorded.CommandId,
                AggregateId = recorded.AggregateId,
                ReceivedOn = DateTime.UtcNow,
                CorrelationId = recorded.CorrelationId,
                GenerationId = recorded.GenerationId,
                ValueDate = recorded.ValueDate,
                Dataset = recorded.Dataset
            };
            await actorContext.SendAsync<RecoveryCanaryAcceptedEvent, ActorEntityId>(accepted)
                .ConfigureAwait(false);
        }, useDurableReplay: false)
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
