using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

/// <summary>
/// Translates Databento wire values into provider-neutral trade semantics at the adapter boundary.
/// </summary>
internal static class DatabentoTradeNormalizer
{
    const byte HeaderSnapshot = 1 << 0;
    const byte HeaderReplay = 1 << 1;
    const byte HeaderUndefinedPrice = 1 << 2;

    const byte DbnLast = 1 << 7;
    const byte DbnTopOfBook = 1 << 6;
    const byte DbnSnapshot = 1 << 5;
    const byte DbnMarketByPrice = 1 << 4;
    const byte DbnBadReceiveTimestamp = 1 << 3;
    const byte DbnMaybeBadBook = 1 << 2;
    const byte DbnPublisherSpecific = 1 << 1;

    /// <summary>Maps a Databento action character to its provider-neutral lifecycle action.</summary>
    /// <param name="action">Databento action byte.</param>
    /// <returns>The corresponding provider-neutral action.</returns>
    internal static NormalizedTradeAction MapAction(byte action) => action switch
    {
        (byte)'A' or (byte)'F' or (byte)'T' => NormalizedTradeAction.New,
        (byte)'M' => NormalizedTradeAction.Change,
        (byte)'C' => NormalizedTradeAction.Cancel,
        (byte)'R' => NormalizedTradeAction.Clear,
        (byte)'N' => NormalizedTradeAction.None,
        _ => NormalizedTradeAction.Unknown
    };

    /// <summary>Maps a Databento aggressor-side character to a provider-neutral side.</summary>
    /// <param name="side">Databento side byte.</param>
    /// <returns>The corresponding provider-neutral side.</returns>
    internal static NormalizedTradeSide MapSide(byte side) => side switch
    {
        (byte)'B' => NormalizedTradeSide.Buy,
        (byte)'A' => NormalizedTradeSide.Sell,
        (byte)'N' => NormalizedTradeSide.Unspecified,
        _ => NormalizedTradeSide.Unknown
    };

    /// <summary>Maps adapter-header and Databento condition bits without leaking provider constants.</summary>
    /// <param name="headerFlags">Flags added by the native adapter.</param>
    /// <param name="dbnFlags">Original Databento record condition flags.</param>
    /// <returns>The combined provider-neutral conditions.</returns>
    internal static NormalizedTradeConditionFlags MapConditions(byte headerFlags, byte dbnFlags)
    {
        var result = NormalizedTradeConditionFlags.None;
        if ((dbnFlags & DbnLast) != 0)
            result |= NormalizedTradeConditionFlags.LastInEvent;
        if ((dbnFlags & DbnTopOfBook) != 0)
            result |= NormalizedTradeConditionFlags.TopOfBook;
        if ((headerFlags & HeaderSnapshot) != 0 || (dbnFlags & DbnSnapshot) != 0)
            result |= NormalizedTradeConditionFlags.Snapshot;
        if ((headerFlags & HeaderReplay) != 0)
            result |= NormalizedTradeConditionFlags.Replay;
        if ((dbnFlags & DbnMarketByPrice) != 0)
            result |= NormalizedTradeConditionFlags.AggregatedPriceLevel;
        if ((dbnFlags & DbnBadReceiveTimestamp) != 0)
            result |= NormalizedTradeConditionFlags.ReceiveTimestampInaccurate;
        if ((dbnFlags & DbnMaybeBadBook) != 0)
            result |= NormalizedTradeConditionFlags.BookMayBeInaccurate;
        if ((dbnFlags & DbnPublisherSpecific) != 0)
            result |= NormalizedTradeConditionFlags.PublisherSpecific;
        if ((headerFlags & HeaderUndefinedPrice) != 0)
            result |= NormalizedTradeConditionFlags.UndefinedPrice;
        return result;
    }
}
