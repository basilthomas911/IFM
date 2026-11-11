using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Streams raw futures trades while the enumerator owns the active provider page.</summary>
public interface IStreamingRawFuturesTradeReader
{
    /// <summary>Reports whether the provider supplies a native streaming read.</summary>
    bool SupportsRawFuturesTradeStreaming => false;

    /// <summary>Reads the original trades in a bounded UTC time segment.</summary>
    /// <param name="contractId">The exact contract identity.</param>
    /// <param name="valueDate">The trading-date partition.</param>
    /// <param name="fromUtc">The inclusive lower UTC time.</param>
    /// <param name="throughUtc">The inclusive upper UTC time.</param>
    /// <param name="token">Cancels the stream.</param>
    /// <returns>The ordered original rows from the active provider page.</returns>
    IAsyncEnumerable<BootstrapTradeRow> StreamRawFuturesTradeWindowAsync(string contractId, DateOnly valueDate,
        TimeOnly fromUtc, TimeOnly throughUtc, CancellationToken token = default);
}
