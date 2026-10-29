using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Existing rollover selection policy over native definition I/O and parsing.
/// The deferred owner performs no acquisition during DI construction.</summary>
public sealed class NativeCurrentFuturesContractResolver(
    Func<TomasAI.IFM.Application.MarketData.Rust.Delivery.NativeMarketDataRuntime> runtime,
    DatabentoMarketDataRuntimeOptions options) : IDatabentoCurrentFuturesContractResolver
{
    /// <inheritdoc/>
    public async Task<ResolvedCurrentFuturesContract> ResolveAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfEqual(valueDate, default);

        var contracts = await ResolveEligibleAsync(symbol, valueDate, 1, cancellationToken)
            .ConfigureAwait(false);
        var contract = contracts[0];
        return new ResolvedCurrentFuturesContract(contract, contract.LastTradeDate);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FuturesContractV3ReadModel>> ResolveEligibleAsync(
        string symbol,
        DateOnly valueDate,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfEqual(valueDate, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var queryOptions = options.FeedOptions with { Dataset = ResolveDataset(normalizedSymbol) };
        var eligibleFrom = valueDate.AddDays(1);
        using var source=await runtime().AcquireDefinitionSnapshotAsync(queryOptions.Dataset,[$"{normalizedSymbol}.FUT"],options.ProviderQueryTimeout,cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return source.SelectCurrentFutures(normalizedSymbol,valueDate,count,options.FuturesContractCurrencyFallbacks.GetValueOrDefault(normalizedSymbol));
    }

    private string ResolveDataset(string symbol)
        =>options.FuturesContractDatasets.TryGetValue(symbol,out var dataset)&&!string.IsNullOrWhiteSpace(dataset)?dataset:string.Equals(symbol,"VX",StringComparison.OrdinalIgnoreCase)?"XCBF.PITCH":options.FeedOptions.Dataset;

}
