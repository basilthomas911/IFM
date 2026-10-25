namespace TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

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
