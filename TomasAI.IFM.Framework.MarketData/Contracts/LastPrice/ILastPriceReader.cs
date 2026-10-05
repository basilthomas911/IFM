namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// A non-consuming, provider-neutral snapshot of the most recently observed
/// trade for one market-data contract.
/// </summary>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="Price">The price snapshot or record price represented by this value.</param>
/// <param name="Size">The size.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="EventTimestamp">The timestamp of the source market event.</param>
/// <param name="ReceiveTimestamp">The timestamp when the record was received.</param>
public readonly record struct LastTradeTickSnapshot(
    string ContractId,
    DateOnly ValueDate,
    decimal Price,
    uint Size,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);

/// <summary>
/// A non-consuming, provider-neutral snapshot of the most recently observed
/// quote for one market-data contract. A missing side remains null.
/// </summary>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="BidPrice">The best bid price.</param>
/// <param name="BidSize">The quantity available at the best bid.</param>
/// <param name="BidCount">The number of orders at the best bid.</param>
/// <param name="AskPrice">The best ask price.</param>
/// <param name="AskSize">The quantity available at the best ask.</param>
/// <param name="AskCount">The number of orders at the best ask.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="EventTimestamp">The timestamp of the source market event.</param>
/// <param name="ReceiveTimestamp">The timestamp when the record was received.</param>
public readonly record struct LastQuoteTickSnapshot(
    string ContractId,
    DateOnly ValueDate,
    decimal? BidPrice,
    uint BidSize,
    uint BidCount,
    decimal? AskPrice,
    uint AskSize,
    uint AskCount,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp)
{
    /// <summary>Local ingestion time, distinct from Databento's provider receive timestamp.</summary>
    public DateTimeOffset LocalReceivedAtUtc { get; init; }

    /// <summary>
    /// Returns a midpoint only for a positive, non-crossed two-sided quote.
    /// </summary>
    /// <param name="midpoint">The midpoint returned by the operation.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryGetMidpoint(out decimal midpoint)
    {
        if (BidPrice is > 0m
            && AskPrice is > 0m
            && BidPrice <= AskPrice)
        {
            midpoint = BidPrice.Value + ((AskPrice.Value - BidPrice.Value) / 2m);
            return true;
        }

        midpoint = default;
        return false;
    }
}

/// <summary>
/// Provides lock-free, non-consuming access to the latest futures trade and
/// quote snapshots held by the active market-data provider.
/// </summary>
public interface IFuturesLastPriceReader
{
    string FuturesContractId { get; }
    DateOnly ValueDate { get; }

    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);
}

/// <summary>
/// Provides lock-free, non-consuming access to the latest futures-option trade
/// and quote snapshots held by the active market-data provider.
/// </summary>
public interface IFuturesOptionLastPriceReader
{
    string FuturesOptionContractId { get; }
    DateOnly ValueDate { get; }

    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);

    /// <summary>
    /// Gets the latest option trade and the quote-derived Greeks state that was
    /// current when the trade was processed.
    /// </summary>
    /// <remarks>
    /// A <see langword="true"/> result means the atomic enriched snapshot is
    /// available; callers must inspect <see cref="OptionGreeksSnapshot.IsValid"/>
    /// and <see cref="OptionGreeksSnapshot.FailureReason"/> separately.
    /// </remarks>
    bool TryGetLastTradeWithGreeks(
        out LastTradeTickWithGreeksSnapshot snapshot);

    /// <summary>
    /// Gets the latest option quote and the Greeks calculation produced for
    /// that exact quote observation.
    /// </summary>
    /// <remarks>
    /// A <see langword="true"/> result means the atomic enriched snapshot is
    /// available; callers must inspect <see cref="OptionGreeksSnapshot.IsValid"/>
    /// and <see cref="OptionGreeksSnapshot.FailureReason"/> separately.
    /// </remarks>
    bool TryGetLastQuoteWithGreeks(
        out LastQuoteTickWithGreeksSnapshot snapshot);
}
