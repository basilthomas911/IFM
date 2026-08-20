using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Seeds and reconciles the minimum futures rollover configuration required by
/// the application before market-data workflows are admitted.
/// </summary>
public sealed class FuturesContractRolloverStartupCheck(
    IMarketDataApi marketDataApi,
    IFuturesContractRolloverStore store,
    TimeProvider timeProvider,
    DatabentoMarketDataRuntimeOptions runtimeOptions,
    IDatabentoContractRegistrationRegistry? registry = null)
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

        if (runtimeOptions.FeedOptions.DataSource != FeedDataSourceMode.Synthetic)
        {
            foreach (var symbol in RequiredSymbols)
            {
                await marketDataApi.UpdateCurrentlyTradedFuturesContractAsync(
                    symbol,
                    valueDate,
                    cancellationToken,
                    forceProviderRefresh: true).ConfigureAwait(false);
            }
        }
        else
        {
            await SeedSyntheticAssignmentsAsync(seeded, cancellationToken)
                .ConfigureAwait(false);
        }

        var validated = await store.GetFuturesContractRolloversAsync(cancellationToken)
            .ConfigureAwait(false);
        List<FuturesContractV2ReadModel> currentContracts = [];
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
            currentContracts.Add(contract);
        }
        registry?.ReplaceCurrentFuturesContracts(currentContracts);
        return validated;
    }

    private async Task SeedSyntheticAssignmentsAsync(
        IReadOnlyCollection<FuturesContractRolloverReadModel> seeded,
        CancellationToken cancellationToken)
    {
        foreach (var symbol in RequiredSymbols)
        {
            var row = seeded.Single(candidate =>
                string.Equals(candidate.Symbol, symbol, StringComparison.Ordinal));
            var persisted = string.IsNullOrWhiteSpace(row.ContractId)
                ? null
                : await store.GetPersistedFuturesContractAsync(
                    row.ContractId, cancellationToken).ConfigureAwait(false);
            if (row.NextRolloverDate is not null
                && persisted is not null
                && persisted.CurrentlyTraded
                && string.Equals(persisted.Symbol, symbol, StringComparison.Ordinal))
                continue;

            var registration = runtimeOptions.Contracts.FirstOrDefault(candidate =>
                candidate.AssetTypeId == AssetTypeId.Futures
                && string.Equals(
                    string.IsNullOrWhiteSpace(candidate.RootSymbol)
                        ? new FuturesContractIdParser(candidate.DomainContractId).Symbol
                        : candidate.RootSymbol,
                    symbol,
                    StringComparison.OrdinalIgnoreCase));
            if (registration is null)
                continue;

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var contract = SyntheticFuturesContractFactory.Create(registration);
            await store.ReplaceCurrentlyTradedFuturesContractAsync(
                row with
                {
                    ContractId = contract.ContractId,
                    NextRolloverDate = contract.LastTradeDate,
                    UpdatedOn = now,
                    UpdatedBy = nameof(FuturesContractRolloverStartupCheck)
                },
                contract,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
