using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

/// <summary>Persists accepted futures rollover definitions and assignments.</summary>
public interface IFuturesContractRolloverWriteStore
{
    /// <summary>Creates missing symbol authorities without replacing existing assignments.</summary>
    /// <param name="symbols">The distinct normalized root symbols prepared by the owning application.</param>
    /// <param name="createdOnUtc">The caller's UTC origin time.</param>
    /// <param name="createdBy">The caller's origin identity.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the missing-row writes.</returns>
    Task EnsureFuturesContractRolloverRowsAsync(IReadOnlyCollection<string> symbols, DateTime createdOnUtc, string createdBy, CancellationToken cancellationToken = default);
}
