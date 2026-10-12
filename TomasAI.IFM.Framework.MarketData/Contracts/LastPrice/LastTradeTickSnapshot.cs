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
