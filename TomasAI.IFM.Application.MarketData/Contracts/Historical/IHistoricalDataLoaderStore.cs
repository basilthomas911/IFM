using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Persists data load checkpoints and immutable manifests in PostgreSQL.</summary>
public interface IHistoricalDataLoaderStore
{
    /// <summary>Gets an attempt by its stable identity.</summary>
    ValueTask<HistoricalDataLoaderState?> GetAsync(Guid attemptId, CancellationToken cancellationToken);
    /// <summary>Gets an already-completed attempt for a request hash.</summary>
    ValueTask<HistoricalDataLoaderState?> GetCompletedByRequestHashAsync(
        string requestSha256,
        CancellationToken cancellationToken);
    /// <summary>Creates or advances an attempt atomically.</summary>
    ValueTask SaveAsync(HistoricalDataLoaderState state, CancellationToken cancellationToken);
}
