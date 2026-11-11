using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Coordinates the explicit operator request to repopulate symbol projections.</summary>
public sealed class SecuritiesSymbolProjectionMaintenance(ISecuritiesDbWriteContext database)
{
    /// <summary>Validates the maintenance request and executes one atomic PostgreSQL copy.</summary>
    /// <param name="batchSize">The compatible positive batch size supplied by the migration tool.</param>
    /// <param name="cancellationToken">Cancels the maintenance operation.</param>
    /// <param name="staleOperationCutoffUtc">An optional UTC cutoff accepted by the legacy tool contract. PostgreSQL needs no journal cleanup.</param>
    /// <returns>The counts of copied futures and option rows.</returns>
    public Task<SecuritiesProjectionBackfillResult> BackfillSymbolProjectionsAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (staleOperationCutoffUtc is { } cutoff)
        {
            if (cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The stale-operation cutoff must have DateTimeKind.Utc.", nameof(staleOperationCutoffUtc));
            if (cutoff > DateTime.UtcNow)
                throw new ArgumentOutOfRangeException(nameof(staleOperationCutoffUtc), cutoff, "The stale-operation cutoff cannot be in the future.");
        }
        return database.ReplaceSymbolProjectionRowsFromCanonicalAsync(cancellationToken);
    }
}
