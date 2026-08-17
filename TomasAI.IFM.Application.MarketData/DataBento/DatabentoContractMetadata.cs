using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

internal static class DatabentoContractMetadata
{
    internal static string ResolveCurrency(
        ContractDetail detail,
        string contractId,
        string? configuredFallback = null)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var currency = string.IsNullOrWhiteSpace(detail.Currency)
            ? detail.SettlementCurrency
            : detail.Currency;
        if (string.IsNullOrWhiteSpace(currency))
            currency = configuredFallback;
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new MarketDataContractMappingException(
                contractId,
                "both the contract currency and settlement currency are missing");
        }

        return currency.Trim().ToUpperInvariant();
    }

    internal static string? FindCurrencyFallback(
        DatabentoMarketDataRuntimeOptions options,
        string symbol)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(symbol))
            return null;
        return options.FuturesContractCurrencyFallbacks.TryGetValue(
            symbol.Trim(),
            out var currency)
                ? currency
                : null;
    }
}
