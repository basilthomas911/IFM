using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function;

/// <summary>Validates a recovery record and submits it to the incident's command actor.</summary>
internal static class RecordMarketDataFeedRecovery
{
    extension(RecordMarketDataFeedRecoveryCommand request)
    {
        /// <summary>Checks the immutable fact and the delivery route and deadline.</summary>
        internal List<ValidationError> Validate()
        {
            var record = RecoveryRecord.Parse(request.Payload, request.Digest);
            if (request.EntityId.Value != record.RecordId || request.AttemptId == Guid.Empty || request.CommandId == Guid.Empty
                || request.RequestDeadlineUtc.Kind != DateTimeKind.Utc || request.AttemptStartedAtUtc.Kind != DateTimeKind.Utc
                || request.RequestDeadlineUtc <= request.AttemptStartedAtUtc
                || request.Subject != new ActorSubject(ActorType.Function, RecordMarketDataFeedRecoveryCommand.Actor,
                    RecordMarketDataFeedRecoveryCommand.Verb, request.EntityId.Format()))
                throw new InvalidDataException("Invalid audit delivery identity/deadline/route.");
            return [];
        }

        /// <summary>Returns the completion event once the owning command acknowledges persistence.</summary>
        internal async ValueTask<MarketDataFeedRecoveryRecordStoredEvent> ExecuteAsync(
            IActorService actors, ILogger logger, CancellationToken token)
        {
            var record = RecoveryRecord.Parse(request.Payload, request.Digest);
            var operation = new MarketDataFeedRecoveryId(record.OperationId);
            var append = new AppendMarketDataFeedRecoveryCommand
            {
                CommandId = request.CommandId,
                EntityId = operation,
                Subject = new(ActorType.Command, AppendMarketDataFeedRecoveryCommand.Actor,
                    AppendMarketDataFeedRecoveryCommand.Verb, operation.Format()),
                Payload = request.Payload,
                Digest = request.Digest,
                AttemptId = request.AttemptId,
                AttemptStartedAtUtc = request.AttemptStartedAtUtc,
                RequestDeadlineUtc = request.RequestDeadlineUtc,
                ReceivedAtUtc = request.ReceivedAtUtc
            };
            var saved = await actors.RequestAsync<AppendMarketDataFeedRecoveryCommand, MarketDataFeedRecoveryId>(append, token)
                .ConfigureAwait(false);
            if (!saved.Success)
                throw new InvalidOperationException(saved.ErrorMessage);

            var receivedOn = DateTime.UtcNow;
            logger.LogDebug("Recovery fact storage acknowledged {RecordId} {OperationId} {Sequence} {Transition} {OccurredAtUtc} {ReceivedAtUtc} {CompletionScope} {MonitoringReady}",
                record.RecordId, record.OperationId, record.Sequence, record.Kind, record.OccurredAtUtc,
                receivedOn, record.CompletionScope, record.MonitoringReady);
            return new()
            {
                EntityId = request.EntityId,
                RecordId = record.RecordId,
                OperationId = record.OperationId,
                Digest = request.Digest,
                Sequence = record.Sequence,
                CommandId = request.CommandId,
                Id = record.RecordId,
                ReceivedOn = receivedOn,
                Subject = new(ActorType.Event, "MarketDataFeedRecoveryEvent", "RecordStored", request.EntityId.Format())
            };
        }
    }

    extension(FunctionEventContext<RecordMarketDataFeedRecoveryCommand> input)
    {
        /// <summary>Preserves the distinction between a conflicting record and a delivery failure.</summary>
        internal MarketDataFeedRecoveryRecordStorageFailedEvent CreateFailure(TimeProvider clock)
        {
            var exception = input.IsConflict
                ? new InvalidDataException("Recovery RecordId content conflict.")
                : input.Exception!;
            var now = clock.GetUtcNow().UtcDateTime;
            return new()
            {
                EntityId = input.Request?.EntityId ?? new(Guid.Empty),
                CommandId = input.Request?.CommandId ?? Guid.Empty,
                ErrorDate = now,
                ReceivedOn = now,
                ErrorMessage = exception.Message,
                ErrorData = exception.ToString(),
                Subject = new(ActorType.Event, "MarketDataFeedRecoveryEvent", "RecordStorageFailed",
                    input.Request?.EntityId.Format() ?? "unknown")
            };
        }
    }
}
