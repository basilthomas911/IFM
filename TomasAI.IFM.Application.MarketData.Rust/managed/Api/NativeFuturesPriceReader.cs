using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>
/// Stable API-epoch futures reader. Each read resolves the current transport book;
/// resetting a dataset clears old values but preserves this public reader handle,
/// matching the current C# store. Disposing the date-scoped runtime retires it.
/// No subscription is acquired by constructing or reading this object.
/// </summary>
public sealed class NativeFuturesPriceReader:IFuturesLastPriceReader {
    readonly NativeMarketDataRuntime runtime;
    readonly MarketDataLane lane;
    /// <inheritdoc/>
    public string FuturesContractId{get;}
    /// <inheritdoc/>
    public DateOnly ValueDate{get;}
    /// <summary>Creates a reader bound to an explicitly configured API date.</summary>
    public NativeFuturesPriceReader(NativeMarketDataRuntime runtime,MarketDataLane lane,string contractId,DateOnly valueDate){
        ArgumentNullException.ThrowIfNull(runtime);ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if(lane is not(MarketDataLane.FuturesGlbx or MarketDataLane.FuturesXcbf))throw new ArgumentOutOfRangeException(nameof(lane));
        if(valueDate==default||runtime.LiveValueDate!=valueDate)throw new ArgumentOutOfRangeException(nameof(valueDate));
        _=new TomasAI.IFM.Domain.MarketData.Shared.FuturesContractIdParser(contractId);
        this.runtime=runtime;this.lane=lane;FuturesContractId=contractId;ValueDate=valueDate;
    }
    bool Read(bool quote,out LiveObservation value){
        // A retired API epoch is unavailable; unexpected native failures are not
        // swallowed as a plausible empty/healthy quote.
        try{value=runtime.ReadLiveObservation(lane,FuturesContractId,quote);return(value.Flags&1)!=0;}catch(ObjectDisposedException){value=default;return false;}
    }
    /// <inheritdoc/>
    public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot){
        if(!Read(false,out var value)){snapshot=default;return false;}
        snapshot=new(FuturesContractId,ValueDate,value.Price/1_000_000_000m,value.Quantity,value.Sequence,DateTimeOffset.UnixEpoch.AddTicks(value.EventTicks),DateTimeOffset.UnixEpoch.AddTicks(value.ReceiveTicks));return true;
    }
    /// <inheritdoc/>
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot){
        if(!Read(true,out var value)){snapshot=default;return false;}
        snapshot=new(FuturesContractId,ValueDate,(value.Flags&2)!=0?value.Bid/1_000_000_000m:null,value.BidQuantity,value.BidCount,(value.Flags&4)!=0?value.Ask/1_000_000_000m:null,value.AskQuantity,value.AskCount,value.Sequence,DateTimeOffset.UnixEpoch.AddTicks(value.EventTicks),DateTimeOffset.UnixEpoch.AddTicks(value.ReceiveTicks)){LocalReceivedAtUtc=DateTimeOffset.UnixEpoch.AddTicks(value.LocalTicks)};return true;
    }
}
