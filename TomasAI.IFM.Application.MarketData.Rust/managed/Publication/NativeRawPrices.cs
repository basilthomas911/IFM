using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Complete copied trade query, already converted into public price units by Rust.</summary>
[MessagePackObject]
public sealed record NativeRawTrade(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] decimal Price,
    [property: Key(3)] uint Size,
    [property: Key(4)] long Sequence,
    [property: Key(5)] DateTimeOffset EventAt,
    [property: Key(6)] DateTimeOffset ReceivedAt)
{
    /// <summary>Materializes the existing public value without arithmetic.</summary>
    public LastTradeTickSnapshot ToSnapshot() => new(ContractId,ValueDate,Price,Size,Sequence,EventAt,ReceivedAt);
}
/// <summary>Complete copied quote query. Missing sides remain null.</summary>
[MessagePackObject]
public sealed record NativeRawQuote(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] decimal? Bid,
    [property: Key(3)] uint BidSize,
    [property: Key(4)] uint BidCount,
    [property: Key(5)] decimal? Ask,
    [property: Key(6)] uint AskSize,
    [property: Key(7)] uint AskCount,
    [property: Key(8)] long Sequence,
    [property: Key(9)] DateTimeOffset EventAt,
    [property: Key(10)] DateTimeOffset ReceivedAt,
    [property: Key(11)] DateTimeOffset LocalAt)
{
    /// <summary>Materializes the existing public value without midpoint or pricing work.</summary>
    public LastQuoteTickSnapshot ToSnapshot() => new(ContractId,ValueDate,Bid,BidSize,BidCount,Ask,AskSize,AskCount,Sequence,EventAt,ReceivedAt){LocalReceivedAtUtc=LocalAt};
}
/// <summary>One coherent native source read. This owns copied values only.</summary>
[MessagePackObject]
public sealed record NativeRawPrices(
    [property: Key(0)] NativeRawTrade? Trade,
    [property: Key(1)] NativeRawQuote? Quote,
    [property: Key(2)] Guid Generation);
