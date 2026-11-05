using System.Security.Cryptography;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.EventProjector;

/// <summary>Collects broker evidence after the resynchronization intent commits.</summary>
internal static class BrokerAccountResynchronizationProducer
{
    private static readonly TimeSpan RequestBudget = TimeSpan.FromSeconds(30);

    internal static async ValueTask<EventProjectionApplyResult> SendAsync(
        BrokerAccountResynchronizationRequestedEvent request, ITradeBroker broker,
        IActorService actorService, IEventSourceActorDbContext eventSource, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var subject = new ActorSubject(ActorType.Event, BrokerAccountActorNames.Event,
                BrokerAccountResynchronizationPreparationEvent.Verb, request.CommandId.ToString("N"));
            var streamId = await eventSource.GetEventStreamIdAsync(subject.StreamId, deadline.Token).ConfigureAwait(false);
            BrokerAccountResynchronizationPreparationEvent? preparation = null;
            await eventSource.MapReduceActorEventStreamFromCreationAsync<BrokerAccountCommandState>(
                streamId, rows => preparation = rows.LastOrDefault()?.ToDomainEvent() as BrokerAccountResynchronizationPreparationEvent,
                deadline.Token).ConfigureAwait(false);

            if (preparation is { Evidence: null })
                return new(EventProjectionApplyOutcome.Failed, "BrokerAccount.RESYNC.OUTCOME_UNKNOWN");
            if (preparation is { Accepted: bool accepted })
                return accepted
                    ? new(EventProjectionApplyOutcome.AlreadyApplied)
                    : new(EventProjectionApplyOutcome.Failed, preparation.FailureReason);

            var remaining = RequestBudget - (DateTime.UtcNow - request.RequestedAtUtc);
            if (remaining <= TimeSpan.Zero || remaining > RequestBudget)
                return new(EventProjectionApplyOutcome.Failed, "BrokerAccount.RESYNC.DEADLINE_EXPIRED");
            deadline.CancelAfter(remaining);

            if (preparation is null)
            {
                if (!string.Equals(request.EntityId.AccountAlias, broker.AccountAlias, StringComparison.Ordinal))
                    return new(EventProjectionApplyOutcome.Failed, "BrokerAccount.RESYNC.ACCOUNT_MISMATCH");

                var attempt = new BrokerAccountResynchronizationPreparationEvent
                {
                    Id = Guid.NewGuid(),
                    EntityId = request.EntityId,
                    Subject = subject,
                    CommandId = request.CommandId,
                    AggregateId = subject.EntityId,
                    EventSource = nameof(BrokerAccountResynchronizationProducer),
                    ReceivedOn = DateTime.UtcNow,
                    Environment = broker.Environment
                };
                await eventSource.SaveEventsAsync(subject.StreamId, request.CommandId, new DomainEventCollection([attempt]),
                    0, deadline.Token).ConfigureAwait(false);

                var snapshot = await broker.ResynchronizeAccountAsync(deadline.Token).AsTask()
                    .WaitAsync(deadline.Token).ConfigureAwait(false);
                if (!string.Equals(snapshot.AccountAlias, request.EntityId.AccountAlias, StringComparison.Ordinal) ||
                    snapshot.AsOfUtc.Kind != DateTimeKind.Utc || snapshot.AsOfUtc < request.RequestedAtUtc ||
                    snapshot.Generation <= 0 || attempt.Environment == BrokerEnvironment.Unknown)
                    return new(EventProjectionApplyOutcome.Failed, "BrokerAccount.RESYNC.INVALID_EVIDENCE");

                preparation = attempt with
                {
                    Id = Guid.NewGuid(),
                    Evidence = BrokerAccountSnapshotEvidence.From(snapshot)
                };
                await eventSource.SaveEventsAsync(subject.StreamId, request.CommandId, new DomainEventCollection([preparation]),
                    1, deadline.Token).ConfigureAwait(false);
            }

            var evidence = preparation.Evidence!;
            var command = new RecordBrokerAccountSnapshotCommand
            {
                CommandId = PreparedCommandId(request.CommandId),
                EntityId = request.EntityId,
                Subject = new(ActorType.Command, BrokerAccountActorNames.Command, RecordBrokerAccountSnapshotCommand.Verb,
                    request.EntityId.Format()),
                Environment = preparation.Environment,
                Snapshot = evidence
            };
            if (await eventSource.GetCommandLogAsync(command.CommandId).WaitAsync(deadline.Token).ConfigureAwait(false) is not null ||
                await eventSource.HasEventForCommandAsync(command.CommandId).WaitAsync(deadline.Token).ConfigureAwait(false))
                return new(EventProjectionApplyOutcome.Failed, "BrokerAccount.RESYNC.HANDOFF_OUTCOME_UNKNOWN");

            var result = await actorService.SendAsync<RecordBrokerAccountSnapshotCommand, BrokerAccountId>(
                command, request.EntityId, deadline.Token).ConfigureAwait(false);
            var receipt = preparation with
            {
                Id = Guid.NewGuid(),
                Accepted = result.Success,
                FailureReason = result.ErrorMessage ?? string.Empty
            };
            await eventSource.SaveEventsAsync(subject.StreamId, request.CommandId, new DomainEventCollection([receipt]),
                2, deadline.Token).ConfigureAwait(false);
            return result.Success
                ? new(EventProjectionApplyOutcome.Applied)
                : new(EventProjectionApplyOutcome.Failed, receipt.FailureReason);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(EventProjectionApplyOutcome.Failed, "BrokerAccount.RESYNC.DEADLINE_EXPIRED");
        }
    }

    private static Guid PreparedCommandId(Guid requestId)
    {
        Span<byte> identity = stackalloc byte[16 + 20];
        requestId.TryWriteBytes(identity);
        "broker-resync-record"u8.CopyTo(identity[16..]);
        return new Guid(SHA256.HashData(identity).AsSpan(0, 16));
    }
}
