using System.Globalization;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed class DatabentoCurrentFuturesContractResolver(
    IDatabentoFeedFactory feeds,
    DatabentoMarketDataRuntimeOptions options) : IDatabentoCurrentFuturesContractResolver
{
    public async Task<ResolvedCurrentFuturesContract> ResolveAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));

        var contracts = await ResolveEligibleAsync(symbol, valueDate, 1, cancellationToken)
            .ConfigureAwait(false);
        var contract = contracts[0];
        return new ResolvedCurrentFuturesContract(contract, contract.LastTradeDate);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FuturesContractV2ReadModel>> ResolveEligibleAsync(
        string symbol,
        DateOnly valueDate,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (valueDate == default) throw new ArgumentOutOfRangeException(nameof(valueDate));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var queryOptions = options.FeedOptions with { Dataset = ResolveDataset(normalizedSymbol) };
        var eligibleFrom = valueDate.AddDays(1);
        var details = await Task.Run(
            () => feeds.CreateMarketDataQueries(queryOptions)
                .GetContractDetails($"{normalizedSymbol}.FUT", options.ProviderQueryTimeout),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var selected = details
            .Where(static detail => detail.ContractKind == ContractKind.Future)
            .Select(detail => new { Detail = detail, Maturity = GetMaturity(detail) })
            .Where(candidate => candidate.Maturity is not null && candidate.Maturity.Value >= eligibleFrom)
            .OrderBy(candidate => candidate.Maturity)
            .ThenBy(candidate => candidate.Detail.RawSymbol, StringComparer.Ordinal)
            .Take(count)
            .ToArray();
        if (selected.Length < count)
            throw new CurrentlyTradedFuturesContractNotFoundException(normalizedSymbol, valueDate);
        return selected.Select(candidate =>
        {
            var maturity = candidate.Maturity!.Value;
            var detail = candidate.Detail;
            var contractId = $"{normalizedSymbol}{maturity:yyyyMMdd}";
            return new FuturesContractV2ReadModel(
                contractId, detail.RawSymbol, normalizedSymbol, detail.RawSymbol, "FUT",
                DatabentoContractMetadata.ResolveCurrency(detail, contractId,
                    DatabentoContractMetadata.FindCurrencyFallback(options, normalizedSymbol)),
                detail.Exchange,
                (detail.ContractMultiplier ?? 1).ToString(CultureInfo.InvariantCulture),
                maturity,
                true);
        }).ToArray();
    }

    private string ResolveDataset(string symbol)
        => DatabentoDatasetSelection.Resolve(options, symbol);

    private static DateOnly? GetMaturity(ContractDetail detail)
    {
        if (detail.MaturityDate is { } maturity)
            return maturity;
        if (detail.ExpirationTimestampNanoseconds is not { } nanoseconds)
            return null;
        var seconds = checked((long)(nanoseconds / 1_000_000_000UL));
        return DateOnly.FromDateTime(
            DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);
    }
}
