using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.Persistence;

/// <summary>Routes accepted pricing events to typed persistence without recalculating their facts.</summary>
public static class AcceptedOptionPricerProjection
{
    /// <summary>Materializes the source event using the caller-owned PostgreSQL transaction.</summary>
    public static Task ProjectAsync(IEnlistedPostgresTransaction transaction, IEvent source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        return source switch
        {
            SpreadDistributionInsertedEvent inserted => transaction.PersistAcceptedDistributionPairAsync(inserted.PutSpreadDistribution, inserted.CallSpreadDistribution, source.EventId, cancellationToken),
            SpreadDistributionDeletedEvent deleted => transaction.DeleteAcceptedDistributionsAsync(deleted.EntityId.TradeId, deleted.EntityId.ValueDate, cancellationToken),
            SpreadDistributionJobSubmittedEvent submitted => transaction.PersistAcceptedDistributionJobAsync(submitted.SpreadDistributionJob, cancellationToken),
            SpreadDistributionJobsInProgressDeletedEvent deleted => transaction.DeleteAcceptedDistributionJobsAsync(deleted.EntityId.OrderId, deleted.EntityId.TradeId, cancellationToken),
            SpreadDistributionJobStatusUpdatedEvent changed => transaction.UpdateAcceptedDistributionJobStatusAsync(changed.EntityId.OrderId, changed.EntityId.TradeId, changed.EntityId.ValueDate, changed.JobStatus, changed.ReceivedOn, null, cancellationToken),
            _ => throw new InvalidOperationException("Unexpected option-pricer source event.")
        };
    }
}
