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

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var dataset = ResolveDataset(normalizedSymbol);
        var queryOptions = options.FeedOptions with { Dataset = dataset };
        var eligibleFrom = valueDate.AddDays(1);
        var details = await Task.Run(
            () => feeds.CreateMarketDataQueries(queryOptions)
                .GetContractDetails($"{normalizedSymbol}.FUT", options.ProviderQueryTimeout),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var selected = details
            .Where(static detail => detail.ContractKind == ContractKind.Future)
            .Select(detail => new { Detail = detail, Maturity = GetMaturity(detail) })
            .Where(candidate => candidate.Maturity is not null
                && candidate.Maturity.Value >= eligibleFrom)
            .OrderBy(candidate => candidate.Maturity)
            .ThenBy(candidate => candidate.Detail.RawSymbol, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new CurrentlyTradedFuturesContractNotFoundException(
                normalizedSymbol, valueDate);

        var maturity = selected.Maturity!.Value;
        var detail = selected.Detail;
        var contractId = $"{normalizedSymbol}{maturity:yyyyMMdd}";
        var contract = new FuturesContractV2ReadModel(
            contractId,
            detail.RawSymbol,
            normalizedSymbol,
            detail.RawSymbol,
            "FUT",
            detail.Currency,
            detail.Exchange,
            (detail.ContractMultiplier ?? 1).ToString(CultureInfo.InvariantCulture),
            maturity,
            true);
        return new ResolvedCurrentFuturesContract(contract, maturity);
    }

    private string ResolveDataset(string symbol)
    {
        if (options.FuturesContractDatasets.TryGetValue(symbol, out var configured)
            && !string.IsNullOrWhiteSpace(configured))
            return configured;
        return string.Equals(symbol, "VX", StringComparison.Ordinal)
            ? "XCBF.PITCH"
            : options.FeedOptions.Dataset;
    }

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
