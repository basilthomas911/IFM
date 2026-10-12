using System.Collections;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

internal static class DatabentoDatasetSelection
{
    // XCBF must establish its live session before GLBX. The Databento gateway can
    // leave an XCBF subscription waiting for symbol mappings/acknowledgements when
    // it is opened after an already-running GLBX session in the same process.
    internal static int StartupPriority(string dataset) =>
        string.Equals(dataset, "XCBF.PITCH", StringComparison.Ordinal)
            ? 0
            : 1;

    internal static string Resolve(
        DatabentoMarketDataRuntimeOptions options,
        string symbol)
    {
        if (options.FuturesContractDatasets.TryGetValue(symbol, out var configured)
            && !string.IsNullOrWhiteSpace(configured))
            return configured;
        return string.Equals(symbol, "VX", StringComparison.OrdinalIgnoreCase)
            ? "XCBF.PITCH"
            : options.FeedOptions.Dataset;
    }

    internal static string Resolve(
        DatabentoMarketDataRuntimeOptions options,
        DatabentoContractRegistration registration) =>
        !string.IsNullOrWhiteSpace(registration.Dataset)
            ? registration.Dataset
            : options.FeedOptions.Dataset;
}
