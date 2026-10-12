using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public interface IDatabentoCurrentFuturesContractResolver
{
    Task<ResolvedCurrentFuturesContract> ResolveAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves the requested number of eligible contracts in expiry order.</summary>
    async Task<IReadOnlyList<FuturesContractV3ReadModel>> ResolveEligibleAsync(
        string symbol,
        DateOnly valueDate,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var current = await ResolveAsync(symbol, valueDate, cancellationToken).ConfigureAwait(false);
        return [current.Contract];
    }
}
