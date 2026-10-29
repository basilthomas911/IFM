using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Exact application wire assembly from owned source integers. Source
/// nanoseconds, original condition bytes and undefined-price sentinels are kept;
/// no hot-cache timestamp or financial recalculation is substituted.</summary>
internal static class NativeWireEvents
{
    static unsafe long F(LiveMarketEvent source,int i)=>source.Fields[i];
    static byte Flags(LiveMarketEvent source)=>checked((byte)(F(source,4)>>16));
    static decimal? Price(long raw)=>raw==long.MaxValue?null:raw/1_000_000_000m;
    internal static FuturesTickQuoteData Quote(LiveMarketEvent source)=>new(checked((uint)F(source,0)),F(source,1),F(source,2),Flags(source),
        F(source,5),Price(F(source,5)),checked((uint)F(source,7)),checked((uint)F(source,9)),F(source,6),Price(F(source,6)),checked((uint)F(source,8)),checked((uint)F(source,10)));
    internal static FuturesTickTradeData Trade(LiveMarketEvent source)=>new(checked((uint)F(source,0)),F(source,1),F(source,2),Flags(source),F(source,5),F(source,5)/1_000_000_000m,
        checked((uint)F(source,6)),checked((byte)F(source,7)),checked((byte)F(source,8)),checked((byte)F(source,9)));
}
