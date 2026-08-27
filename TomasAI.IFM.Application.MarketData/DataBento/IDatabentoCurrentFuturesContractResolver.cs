using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed record ResolvedCurrentFuturesContract(
    FuturesContractV2ReadModel Contract,
    DateOnly NextRolloverDate);

public interface IDatabentoCurrentFuturesContractResolver
{
    Task<ResolvedCurrentFuturesContract> ResolveAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves the requested number of eligible contracts in expiry order.</summary>
    async Task<IReadOnlyList<FuturesContractV2ReadModel>> ResolveEligibleAsync(
        string symbol,
        DateOnly valueDate,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var current = await ResolveAsync(symbol, valueDate, cancellationToken).ConfigureAwait(false);
        return [current.Contract];
    }
}
