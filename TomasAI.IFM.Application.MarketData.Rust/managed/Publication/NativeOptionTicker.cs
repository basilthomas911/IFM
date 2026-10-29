using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.Rust.Publication;
/// <summary>Private indexed wire mirror; all values and joins were computed in Rust.</summary>
[MessagePackObject]
public sealed record NativeOptionTicker([property:Key(0)]NativeTickerPrice Price,[property:Key(1)]NativeQueryGreeks? Greeks)
{
    /// <summary>Constructs the public immutable value without arithmetic or qualification.</summary>
    public OptionTickerPriceSnapshot ToSnapshot()=>new(Price.ToSnapshot(),Greeks?.ToSnapshot());
}
/// <summary>Exact native publication-price ticker payload.</summary>
[MessagePackObject]
public sealed record NativeTickerPrice([property:Key(0)]string ContractId,[property:Key(1)]uint InstrumentId,
 [property:Key(2)]ushort PublisherId,[property:Key(3)]int Asset,[property:Key(4)]DateOnly ValueDate,
 [property:Key(5)]NativeTickerQuote? Quote,[property:Key(6)]NativeTickerTrade? Trade)
{
    /// <summary>Copies native fields only.</summary>
    public TickerPriceSnapshot ToSnapshot()=>new(ContractId,InstrumentId,PublisherId,(AssetTypeId)Asset,ValueDate,Quote?.ToSnapshot(),Trade?.ToSnapshot());
}
/// <summary>Exact indexed quote transfer, including original provider times.</summary>
[MessagePackObject]
public sealed record NativeTickerQuote([property:Key(0)]decimal? Bid,[property:Key(1)]uint BidSize,
 [property:Key(2)]decimal? Ask,[property:Key(3)]uint AskSize,[property:Key(4)]uint BidCount,[property:Key(5)]uint AskCount,
 [property:Key(6)]long Sequence,[property:Key(7)]DateTimeOffset EventAt,[property:Key(8)]DateTimeOffset ReceivedAt)
{
    /// <summary>Copies native fields only.</summary>
    public TickerQuoteSnapshot ToSnapshot()=>new(Bid,BidSize,Ask,AskSize,BidCount,AskCount,Sequence,EventAt,ReceivedAt);
}
/// <summary>Exact indexed trade transfer; native suppression preserves its source identity.</summary>
[MessagePackObject]
public sealed record NativeTickerTrade([property:Key(0)]decimal Price,[property:Key(1)]uint Size,
 [property:Key(2)]long Sequence,[property:Key(3)]DateTimeOffset EventAt,[property:Key(4)]DateTimeOffset ReceivedAt)
{
    /// <summary>Copies native fields only.</summary>
    public TickerTradeSnapshot ToSnapshot()=>new(Price,Size,Sequence,EventAt,ReceivedAt);
}
