using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Seeds and reconciles the minimum futures rollover configuration required by
/// the application before market-data workflows are admitted.
/// </summary>
public sealed class FuturesContractRolloverStartupCheck(
    IMarketDataApi marketDataApi,
    IFuturesContractRolloverStore store,
    TimeProvider timeProvider)
{
    public static readonly string[] RequiredSymbols = ["ES", "VX"];

    public async Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> ExecuteAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));

        await store.EnsureFuturesContractRolloverRowsAsync(
            RequiredSymbols,
            timeProvider.GetUtcNow().UtcDateTime,
            nameof(FuturesContractRolloverStartupCheck),
            cancellationToken).ConfigureAwait(false);

        var seeded = await store.GetFuturesContractRolloversAsync(cancellationToken)
            .ConfigureAwait(false);
        if (seeded.Count == 0)
        {
            throw new FuturesContractRolloverConfigurationException(
                "The futures_contract_rollover table must contain at least one row.");
        }

        foreach (var symbol in RequiredSymbols)
        {
            await marketDataApi.UpdateCurrentlyTradedFuturesContractAsync(
                symbol, valueDate, cancellationToken).ConfigureAwait(false);
        }

        var validated = await store.GetFuturesContractRolloversAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var requiredSymbol in RequiredSymbols)
        {
            var row = validated.SingleOrDefault(candidate =>
                string.Equals(candidate.Symbol, requiredSymbol, StringComparison.Ordinal));
            if (row is null || string.IsNullOrWhiteSpace(row.ContractId)
                || row.NextRolloverDate is null)
            {
                throw new FuturesContractRolloverConfigurationException(
                    $"The futures-contract rollover row for '{requiredSymbol}' is not valid.");
            }

            var contract = await store.GetPersistedFuturesContractAsync(
                row.ContractId, cancellationToken).ConfigureAwait(false);
            if (contract is null || !contract.CurrentlyTraded
                || !string.Equals(contract.Symbol, row.Symbol, StringComparison.Ordinal))
            {
                throw new FuturesContractRolloverConfigurationException(
                    $"The rollover row for '{requiredSymbol}' does not identify a persisted currently traded contract.");
            }
        }
        return validated;
    }
}
