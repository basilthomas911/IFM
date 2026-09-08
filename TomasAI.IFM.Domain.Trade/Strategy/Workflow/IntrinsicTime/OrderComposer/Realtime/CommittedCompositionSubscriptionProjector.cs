using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;

/// <summary>Durable business projection independent of UI notification and actor subscription lifetimes.</summary>
public sealed class CommittedCompositionSubscriptionProjector(ICommittedBusinessEventJournal journal,
    ICommittedBusinessSubscriptionSource source, IDurableSubscriptionIntentStore store,
    ILogger<CommittedCompositionSubscriptionProjector> logger, CompositionDiscoveryHandoff? handoffs = null) : BackgroundService
{
    readonly SemaphoreSlim serial = new(1, 1);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                await ProjectPendingAsync(stoppingToken).ConfigureAwait(false);
                if (handoffs is not null) await handoffs.CompletePendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Business subscription projection remains pending: {ErrorType}", error.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async Task<int> ProjectPendingAsync(CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await journal.ReadPendingAsync(CommittedCompositionSubscriptionSource.EventNames, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                // Acquire the position before relinquishing its working order, including after a partial/crashed retry.
                var kinds = row.EventName == nameof(WorkflowStrategyStateUpdatedEvent)
                    ? new[] { BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow }
                    : new[] { BusinessSubscriptionSourceKind.TradePosition, BusinessSubscriptionSourceKind.TradeOrder };
                foreach (var kind in kinds)
                {
                    var reference = CommittedCompositionSubscriptionSource.Reference(row, kind);
                    for (var attempt = 0; ; attempt++)
                    {
                        var fact = await source.ReadAsync(reference, cancellationToken).ConfigureAwait(false)
                            ?? throw new InvalidDataException("Committed source is unavailable.");
                        // Old workflows/trades without composition ownership must not exhaust the bounded
                        // authority store during startup replay. Unknown never grants or releases a lease.
                        // Existing owners still receive the fact and retain their exact committed leases.
                        if (fact.Status == DurableAuthorityStatus.Unknown
                            && !(await store.ReadAsync(fact.Scope, fact.Dataset, cancellationToken).ConfigureAwait(false))
                                .Authorities.Any(x => x.SourceId == fact.SourceId)) break;
                        var result = await store.ApplyAsync(fact, cancellationToken).ConfigureAwait(false);
                        if (result.Code is DurableIntentResultCode.Committed or DurableIntentResultCode.AlreadyApplied or DurableIntentResultCode.StaleAuthority) break;
                        if (result.Code != DurableIntentResultCode.RevisionConflict || attempt >= 3)
                            throw new InvalidDataException("Business source projection rejected: " + result.Code);
                    }
                }
                await journal.AcknowledgeAsync(row.EventVersion, cancellationToken).ConfigureAwait(false);
            }
            return rows.Count;
        }
        finally { serial.Release(); }
    }
}
