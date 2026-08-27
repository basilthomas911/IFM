using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.EventProjector;

/// <summary>Projects committed private Regime Discovery events before publishing public terminal events.</summary>
public sealed class RegimeDiscoveryEventProjector
    : ConventionalEventProjector<RegimeDiscoveryCommandActor>
{
    const int SchemaVersion = 1;
    readonly IRegimeDiscoveryCommandContext _context;
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    /// <summary>Initializes the conventional, non-durable Regime Discovery projector.</summary>
    public RegimeDiscoveryEventProjector(
        ICommandActorContext<RegimeDiscoveryCommandActor> actorContext,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(
            Typed(actorContext).DurableReplayQueue,
            Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService,
            Typed(actorContext).Logger,
            reliabilityOptions)
    {
        _context = Typed(actorContext);
        _descriptors =
        [
            Describe<RegimeDiscoveryCalculationCompletedEvent>(),
            Describe<RegimeDiscoveryCalculationFailedEvent>()
        ];
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes
        => _descriptors.Select(static value => value.SourceEventType).ToArray();

    /// <summary>Rebuilds Scylla projections without republishing historical terminal events.</summary>
    public async ValueTask RebuildAsync(
        IEnumerable<IEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var domainEvent in events.OrderBy(static value => value.EventId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProjectAsync(domainEvent, publishTerminal: false, cancellationToken).ConfigureAwait(false);
        }
    }

    EventProjectionDescriptor Describe<TEvent>() where TEvent : class, IEvent
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await ProjectAsync(domainEvent, publishTerminal: true, CancellationToken.None).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            _ => null,
            (_, _) => null,
            publishProcessingEvent: false,
            useDurableReplay: false,
            publishTerminalEvent: false);

    async ValueTask ProjectAsync(IEvent domainEvent, bool publishTerminal, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case RegimeDiscoveryCalculationCompletedEvent completed:
                await ProjectCompletedAsync(completed, cancellationToken).ConfigureAwait(false);
                if (publishTerminal)
                    await PublishCompletedAsync(completed, cancellationToken).ConfigureAwait(false);
                break;
            case RegimeDiscoveryCalculationFailedEvent failed:
                await ProjectFailedAsync(failed, cancellationToken).ConfigureAwait(false);
                if (publishTerminal)
                    await PublishFailedAsync(failed, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Regime Discovery event {domainEvent.GetType().Name}.");
        }
    }

    async ValueTask ProjectCompletedAsync(
        RegimeDiscoveryCalculationCompletedEvent completed,
        CancellationToken cancellationToken)
    {
        var resultPayload = MessagePackSerializer.Serialize(completed.Result);
        var reasonsPayload = MessagePackSerializer.Serialize(completed.Result.Reasons);
        await _context.DbFactory.TradeDb.UpsertRegimeDiscoveryAsync(new RegimeDiscoveryReadModel
        {
            WorkflowId = completed.WorkflowId,
            WorkflowEntityId = completed.EntityId.Format(),
            InputWorkflowRevision = completed.InputWorkflowRevision,
            CommandId = completed.CommandId,
            SourceEventId = completed.Id,
            SourceEventSequence = completed.EventId,
            Status = "Completed",
            ParameterPayloadSha256 = completed.ParameterPayloadSha256,
            SignalSnapshotId = completed.SignalSnapshotId,
            ResultPayload = resultPayload,
            ResultPayloadSha256 = completed.ResultPayloadSha256,
            ReasonsPayload = reasonsPayload,
            SchemaVersion = SchemaVersion,
            TerminalAtUtc = completed.CompletedAtUtc,
            UpdatedAtUtc = completed.ReceivedOn == default ? completed.CompletedAtUtc : completed.ReceivedOn
        }, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask ProjectFailedAsync(
        RegimeDiscoveryCalculationFailedEvent failed,
        CancellationToken cancellationToken)
    {
        await _context.DbFactory.TradeDb.UpsertRegimeDiscoveryAsync(new RegimeDiscoveryReadModel
        {
            WorkflowId = failed.WorkflowId,
            WorkflowEntityId = failed.EntityId.Format(),
            InputWorkflowRevision = failed.InputWorkflowRevision,
            CommandId = failed.CommandId,
            SourceEventId = failed.Id,
            SourceEventSequence = failed.EventId,
            Status = "Failed",
            ParameterPayloadSha256 = failed.ParameterPayloadSha256,
            SignalSnapshotId = failed.SignalSnapshotId,
            ResultPayload = ReadOnlyMemory<byte>.Empty,
            FailureCode = failed.Failure.ErrorCode,
            FailureMessage = failed.Failure.ErrorMessage,
            ReasonsPayload = MessagePackSerializer.Serialize(failed.Reasons),
            SchemaVersion = SchemaVersion,
            TerminalAtUtc = failed.FailedAtUtc,
            UpdatedAtUtc = failed.ReceivedOn == default ? failed.FailedAtUtc : failed.ReceivedOn
        }, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask PublishCompletedAsync(
        RegimeDiscoveryCalculationCompletedEvent completed,
        CancellationToken cancellationToken)
    {
        var payload = MessagePackSerializer.Serialize(completed.Result);
        var terminal = new RegimeDiscoveryPipelineCompletedEvent
        {
            Subject = RealtimeSubject(RegimeDiscoveryPipelineCompletedEvent.Verb, completed.EntityId.Format()),
            Id = completed.Id,
            EntityId = completed.EntityId,
            EventId = completed.EventId,
            CommandId = completed.CommandId,
            AggregateId = completed.AggregateId,
            EventSource = completed.EventSource,
            ReceivedOn = completed.ReceivedOn,
            WorkflowId = completed.WorkflowId,
            InputWorkflowRevision = completed.InputWorkflowRevision,
            CorrelationId = completed.CorrelationId,
            CausationId = completed.CausationId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            Result = StrategyStageResultEnvelope.Create(
                completed.Result.ResultId,
                nameof(RegimeDiscoveryResult),
                SchemaVersion,
                payload,
                completed.Result.MarketDataAsOfUtc,
                completed.Result.ProducedAtUtc),
            CompletedAtUtc = completed.CompletedAtUtc
        };
        await _context.SendAsync<RegimeDiscoveryPipelineCompletedEvent,
            Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowEntityId>(
            terminal, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask PublishFailedAsync(
        RegimeDiscoveryCalculationFailedEvent failed,
        CancellationToken cancellationToken)
    {
        var terminal = new RegimeDiscoveryPipelineFailedEvent
        {
            Subject = RealtimeSubject(RegimeDiscoveryPipelineFailedEvent.Verb, failed.EntityId.Format()),
            EntityId = failed.EntityId,
            Id = failed.Id,
            ErrorDate = failed.FailedAtUtc,
            EventId = failed.EventId,
            CommandId = failed.CommandId,
            EventSource = failed.EventSource,
            ErrorMessage = failed.Failure.ErrorMessage,
            ErrorCode = failed.Failure.ErrorCode,
            ErrorType = ErrorType.Command,
            ErrorData = failed.Failure.ErrorData,
            ReceivedOn = failed.ReceivedOn,
            AggregateId = failed.AggregateId,
            CommandName = nameof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.StartRegimeDiscoveryPipelineCommand),
            RouteTo = BoundedContextName.RegimeDiscoveryPipelineBoundedContext.ToString(),
            WorkflowId = failed.WorkflowId,
            InputWorkflowRevision = failed.InputWorkflowRevision,
            CorrelationId = failed.CorrelationId,
            CausationId = failed.CausationId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery
        };
        await _context.SendAsync<RegimeDiscoveryPipelineFailedEvent,
            Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowEntityId>(
            terminal, cancellationToken).ConfigureAwait(false);
    }

    static ActorSubject RealtimeSubject(string verb, string entityId)
        => new(ActorType.Realtime, RegimeDiscoveryPipelineCompletedEvent.Actor, verb, entityId);

    static IRegimeDiscoveryCommandContext Typed(ICommandActorContext<RegimeDiscoveryCommandActor> context)
        => context as IRegimeDiscoveryCommandContext
           ?? throw new ArgumentException(
               $"{nameof(context)} must implement {nameof(IRegimeDiscoveryCommandContext)}.", nameof(context));
}
