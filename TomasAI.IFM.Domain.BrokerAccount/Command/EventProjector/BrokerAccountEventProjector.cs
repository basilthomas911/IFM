using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.EventProjector;

/// <summary>Projects committed account event payloads into PostgreSQL with durable retry.</summary>
public sealed class BrokerAccountEventProjector(IBrokerAccountProjectionWriter writer, IDurableReplayQueue queue,
    IEventSourceActorDbContext eventSource, ILogger<BrokerAccountEventProjector> logger,
    ITradeBroker broker, IActorService actorService, EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<BrokerAccountCommandActor>(queue, eventSource, logger, options)
{
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        Describe(typeof(BrokerAccountChangedEvent), writer, false),
        Describe(typeof(BrokerAccountOperationAppliedEvent), writer, true),
        new EventProjectionDescriptor(typeof(BrokerAccountResynchronizationRequestedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            (source, execution) => BrokerAccountResynchronizationProducer.SendAsync(
                (BrokerAccountResynchronizationRequestedEvent)source, broker, actorService, eventSource, execution.CancellationToken),
            source => Complete((BrokerAccountResynchronizationRequestedEvent)source),
            (source, error) => Fail((BrokerAccountResynchronizationRequestedEvent)source, error),
            publishProcessingEvent: false).WithReadModelRebuild((_, _) => Task.CompletedTask),
        new EventProjectionDescriptor(typeof(BrokerAccountOperationDecidedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            (source, _) =>
            {
                var decision = (BrokerAccountOperationDecidedEvent)source;
                return ValueTask.FromResult(decision.Accepted
                    ? new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied)
                    : new EventProjectionApplyResult(EventProjectionApplyOutcome.Failed, decision.RejectionReason));
            },
            source => Complete((BrokerAccountOperationDecidedEvent)source),
            (source, error) => Fail((BrokerAccountOperationDecidedEvent)source, error),
            publishProcessingEvent: false).WithReadModelRebuild((_, _) => Task.CompletedTask)
    ];

    public override IReadOnlyCollection<Type> ProjectedEventTypes { get; } =
        [typeof(BrokerAccountChangedEvent), typeof(BrokerAccountOperationAppliedEvent), typeof(BrokerAccountOperationDecidedEvent),
         typeof(BrokerAccountResynchronizationRequestedEvent)];

    private static EventProjectionDescriptor Describe(Type sourceType, IBrokerAccountProjectionWriter writer, bool publishOutcome) =>
        new EventProjectionDescriptor(sourceType, EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (source, execution) =>
            {
                await writer.ProjectAsync(((BrokerAccountChangedEvent)source).BrokerAccountDefinition, execution.CancellationToken)
                    .ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            source => source is BrokerAccountOperationAppliedEvent applied ? Complete(applied) : null,
            (source, error) => source is BrokerAccountOperationAppliedEvent applied ? Fail(applied, error) : null,
            publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: publishOutcome)
            .WithReadModelRebuild(async (source, token) =>
                await writer.ProjectAsync(((BrokerAccountChangedEvent)source).BrokerAccountDefinition, token).ConfigureAwait(false));

    private static ICompleteEvent Complete(BrokerAccountOperationAppliedEvent source) => source.Operation switch
    {
        BrokerAccountOperation.RecordBrokerAccountSnapshot => Outcome<BrokerAccountSnapshotRecordedCompleteEvent>(source),
        BrokerAccountOperation.SubmitAccountQualificationEvidence => Outcome<AccountQualificationEvidenceSubmittedCompleteEvent>(source),
        BrokerAccountOperation.AcceptAccountQualification => Outcome<AccountQualificationAcceptedCompleteEvent>(source),
        BrokerAccountOperation.RevokeAccountQualification => Outcome<AccountQualificationRevokedCompleteEvent>(source),
        BrokerAccountOperation.SetManualTradingHold => Outcome<ManualTradingHoldSetCompleteEvent>(source),
        BrokerAccountOperation.ReleaseManualTradingHold => Outcome<ManualTradingHoldReleasedCompleteEvent>(source),
        BrokerAccountOperation.RequestBrokerAccountResynchronization => Outcome<BrokerAccountResynchronizedCompleteEvent>(source),
        _ => throw new InvalidDataException("Unknown broker account operation.")
    };

    private static IErrorEvent Fail(BrokerAccountOperationAppliedEvent source, Exception error)
    {
        BrokerAccountOperationFail outcome = source.Operation switch
        {
            BrokerAccountOperation.RecordBrokerAccountSnapshot => Outcome<BrokerAccountSnapshotRecordedFailEvent>(source),
            BrokerAccountOperation.SubmitAccountQualificationEvidence => Outcome<AccountQualificationEvidenceSubmittedFailEvent>(source),
            BrokerAccountOperation.AcceptAccountQualification => Outcome<AccountQualificationAcceptedFailEvent>(source),
            BrokerAccountOperation.RevokeAccountQualification => Outcome<AccountQualificationRevokedFailEvent>(source),
            BrokerAccountOperation.SetManualTradingHold => Outcome<ManualTradingHoldSetFailEvent>(source),
            BrokerAccountOperation.ReleaseManualTradingHold => Outcome<ManualTradingHoldReleasedFailEvent>(source),
            BrokerAccountOperation.RequestBrokerAccountResynchronization => Outcome<BrokerAccountResynchronizedFailEvent>(source),
            _ => throw new InvalidDataException("Unknown broker account operation.")
        };
        if (source is BrokerAccountOperationDecidedEvent { Accepted: false } decision)
            return outcome with { ErrorType = ErrorType.Command, ErrorCode = decision.RejectionCode, ErrorMessage = decision.RejectionReason };
        return outcome with { ErrorType = ErrorType.Denormalizer, ErrorCode = 25104, ErrorMessage = error.Message };
    }

    private static T Outcome<T>(BrokerAccountChangedEvent source) where T : BrokerAccountOperationOutcome, new() => new()
    {
        Subject = new(ActorType.Event, BrokerAccountActorNames.Outcome, typeof(T).Name[..^"Event".Length], source.EntityId.Format()),
        Id = source.Id,
        EntityId = source.EntityId,
        CommandId = source.CommandId,
        EventId = source.EventId,
        AggregateId = source.AggregateId,
        EventSource = source.EventSource,
        ReceivedOn = source.ReceivedOn
    };
}
