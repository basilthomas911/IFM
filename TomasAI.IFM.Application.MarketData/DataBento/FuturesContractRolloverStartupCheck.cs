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
            await marketDataApi.UpdateOnTheRunFuturesContractAsync(
                "ES", valueDate, cancellationToken).ConfigureAwait(false);
            var termStructureAvailable = await marketDataApi.UpdateFuturesTermStructureContractsAsync(
                "VX", valueDate, cancellationToken).ConfigureAwait(false);
            if (!termStructureAvailable)
            {
                var persistedVx = await store.GetFuturesRolloverSetAsync(
                    "VX", cancellationToken).ConfigureAwait(false);
                if (persistedVx.Count != 2)
                    throw new FuturesContractRolloverConfigurationException(
                        "DataBento did not resolve the required current-month and next-month VX contracts.");
            }
        }
        else
        {
            await SeedSyntheticAssignmentsAsync(seeded, cancellationToken)
                .ConfigureAwait(false);
        }

        var validated = await store.GetFuturesContractRolloversAsync(cancellationToken)
            .ConfigureAwait(false);
        List<FuturesContractV3ReadModel> currentContracts = [];
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

            var contracts = (await store.GetFuturesRolloverSetAsync(
                    requiredSymbol, cancellationToken).ConfigureAwait(false))
                .OrderBy(static contract => contract.LastTradeDate)
                .ToArray();
            var requiredCount = requiredSymbol == "VX" ? 2 : 1;
            if (contracts.Length != requiredCount
                || contracts.Any(contract => !contract.Rollover
                    || !string.Equals(contract.Symbol, row.Symbol, StringComparison.Ordinal))
                || contracts.Count(static contract => contract.OnTheRun) != 1
                || !contracts[0].OnTheRun
                || !string.Equals(contracts[0].ContractId, row.ContractId, StringComparison.Ordinal))
            {
                throw new FuturesContractRolloverConfigurationException(
                    $"The rollover row for '{requiredSymbol}' does not identify its required persisted rollover set.");
            }
            currentContracts.AddRange(contracts);
        }
        if (registry is not null)
        {
            foreach (var group in currentContracts.GroupBy(
                         static contract => contract.Symbol,
                         StringComparer.Ordinal))
            {
                registry.ReplaceFuturesRolloverSet(
                    group.Key,
                    group.OrderBy(static contract => contract.LastTradeDate).ToArray());
            }
        }
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
            var requiredCount = symbol == "VX" ? 2 : 1;
            var persisted = await store.GetFuturesRolloverSetAsync(
                symbol, cancellationToken).ConfigureAwait(false);
            if (row.NextRolloverDate is not null
                && persisted.Count == requiredCount
                && persisted.All(contract => contract.Rollover
                    && string.Equals(contract.Symbol, symbol, StringComparison.Ordinal))
                && persisted.Count(static contract => contract.OnTheRun) == 1)
                continue;

            var contracts = runtimeOptions.Contracts
                .Where(candidate => candidate.AssetTypeId == AssetTypeId.Futures
                    && string.Equals(
                        string.IsNullOrWhiteSpace(candidate.RootSymbol)
                            ? new FuturesContractIdParser(candidate.DomainContractId).Symbol
                            : candidate.RootSymbol,
                        symbol,
                        StringComparison.OrdinalIgnoreCase))
                .Select(SyntheticFuturesContractFactory.Create)
                .OrderBy(static contract => contract.LastTradeDate)
                .Take(requiredCount)
                .ToArray();
            if (contracts.Length != requiredCount)
                throw new FuturesContractRolloverConfigurationException(
                    $"Synthetic startup requires {requiredCount} configured '{symbol}' futures contract(s).");

            for (var index = 0; index < contracts.Length; index++)
            {
                contracts[index] = contracts[index] with
                {
                    OnTheRun = index == 0,
                    Rollover = true
                };
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            await store.ReplaceFuturesRolloverSetAsync(
                row with
                {
                    ContractId = contracts[0].ContractId,
                    NextRolloverDate = contracts[0].LastTradeDate,
                    UpdatedOn = now,
                    UpdatedBy = nameof(FuturesContractRolloverStartupCheck)
                },
                contracts,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
