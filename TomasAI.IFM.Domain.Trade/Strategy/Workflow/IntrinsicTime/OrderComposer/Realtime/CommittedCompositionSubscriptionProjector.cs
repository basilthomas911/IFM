using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;

public interface ICommittedCompositionSubscriptionProjector
{
    Task ProjectCommittedAsync(IEvent committedEvent, ProjectionExecutionContext context);
}

/// <summary>Projects a delivered committed event without scanning the event store.</summary>
public sealed class CommittedCompositionSubscriptionProjector(ICommittedBusinessEventJournal journal,
    ICommittedBusinessSubscriptionSource source, IDurableSubscriptionIntentStore store,
    ILogger<CommittedCompositionSubscriptionProjector> logger, CompositionDiscoveryHandoff? handoffs = null)
    : ICommittedCompositionSubscriptionProjector
{
    readonly SemaphoreSlim serial = new(1, 1);

    public Task ProjectCommittedAsync(IEvent committedEvent, ProjectionExecutionContext context)
        => ProjectCommittedAsync(committedEvent, context.EventId,
            context.StreamVersion > 0 ? context.StreamVersion : context.EventId, context.EventStreamId,
            context.CancellationToken);

    async Task ProjectCommittedAsync(IEvent committedEvent, long eventId, long sourceVersion,
        long eventStreamId, CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await ApplyAsync(committedEvent, sourceVersion, eventStreamId, eventId, cancellationToken)
                    .ConfigureAwait(false);
                await journal.AcknowledgeAsync(eventId, cancellationToken).ConfigureAwait(false);
                if (committedEvent is WorkflowStrategyStateUpdatedEvent workflow && handoffs is not null)
                    await handoffs.CompleteAsync(workflow, eventId, sourceVersion, cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (InvalidDataException error)
            {
                await journal.RejectAsync(eventId, "InvalidCommittedSource", error.Message, cancellationToken)
                    .ConfigureAwait(false);
                logger.LogError(
                    "Committed business source was quarantined. EventId={EventId} EventName={EventName} Reason={Reason}",
                    eventId, committedEvent.GetType().Name, error.Message);
            }
        }
        finally { serial.Release(); }
    }

    public async Task<int> ProjectPendingAsync(CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await journal.ReadPendingAsync(CommittedCompositionSubscriptionSource.EventNames, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                try
                {
                    var value = row.ToDomainEvent();
                    await ApplyAsync(value, row.StreamVersion > 0 ? row.StreamVersion : row.EventVersion,
                        row.EventStreamId, row.EventVersion, cancellationToken).ConfigureAwait(false);
                }
                catch(InvalidDataException error)
                {
                    await journal.RejectAsync(row.EventVersion,"InvalidCommittedSource",error.Message,cancellationToken).ConfigureAwait(false);
                    logger.LogError("Committed business source was quarantined. EventId={EventId} EventName={EventName} Reason={Reason}",
                        row.EventVersion,row.EventName,error.Message);
                    continue;
                }
                await journal.AcknowledgeAsync(row.EventVersion, cancellationToken).ConfigureAwait(false);
            }
            return rows.Count;
        }
        finally { serial.Release(); }
    }

    async Task ApplyAsync(IEvent committedEvent, long sourceVersion, long eventStreamId, long eventLogId,
        CancellationToken cancellationToken)
    {
        // Acquire the position before relinquishing its working order, including after a partial/crashed retry.
        if (committedEvent is WorkflowStrategyStateUpdatedEvent)
        {
            await ApplyKindAsync(committedEvent, BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow,
                sourceVersion, eventStreamId, eventLogId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ApplyKindAsync(committedEvent, BusinessSubscriptionSourceKind.TradePosition,
            sourceVersion, eventStreamId, eventLogId, cancellationToken).ConfigureAwait(false);
        await ApplyKindAsync(committedEvent, BusinessSubscriptionSourceKind.TradeOrder,
            sourceVersion, eventStreamId, eventLogId, cancellationToken).ConfigureAwait(false);
    }

    async Task ApplyKindAsync(IEvent committedEvent, BusinessSubscriptionSourceKind kind,
        long sourceVersion, long eventStreamId, long eventLogId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var fact = await source.ReadCommittedAsync(committedEvent, kind, sourceVersion, eventStreamId, eventLogId,
                cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("Committed source is unavailable.");
            // Old workflows/trades without composition ownership must not exhaust the bounded authority store.
            if (fact.Status == DurableAuthorityStatus.Unknown
                && !(await store.ReadAsync(fact.Scope, fact.Dataset, cancellationToken).ConfigureAwait(false))
                    .Authorities.Any(x => x.SourceId == fact.SourceId)) return;
            var result = await store.ApplyAsync(fact, cancellationToken).ConfigureAwait(false);
            if (result.Code is DurableIntentResultCode.Committed or DurableIntentResultCode.AlreadyApplied
                or DurableIntentResultCode.StaleAuthority) return;
            if (result.Code != DurableIntentResultCode.RevisionConflict || attempt >= 3)
                throw new InvalidDataException("Business source projection rejected: " + result.Code);
        }
    }
}
