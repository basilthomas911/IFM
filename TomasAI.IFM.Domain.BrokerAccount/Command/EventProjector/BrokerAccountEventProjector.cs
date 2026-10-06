using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.EventProjector;

/// <summary>Projects committed account event payloads into ScyllaDB with durable retry.</summary>
public sealed class BrokerAccountEventProjector(IBrokerAccountProjectionWriter writer, IDurableReplayQueue queue,
    IEventSourceActorDbContext eventSource, IBlackboardService blackboard, ILogger<BrokerAccountEventProjector> logger,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<BrokerAccountCommandActor>(queue, eventSource, blackboard, logger, options)
{
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
        [new EventProjectionDescriptor(typeof(BrokerAccountChangedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, execution) =>
            {
                await writer.ProjectAsync(((BrokerAccountChangedEvent)domainEvent).BrokerAccountDefinition, execution.CancellationToken).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            }, _ => null, (_, _) => null, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: false)];
    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(BrokerAccountChangedEvent)];
}
