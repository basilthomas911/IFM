using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Command-free query adapter. Rust owns source selection, reference
/// qualification and all Greek calculations, including replacement fencing.
/// Keeping this object cannot retain a native dataset lease, socket or task.</summary>
public sealed class NativeOwnedOptionPriceReader : IFuturesOptionLastPriceReader
{
    readonly NativeMarketDataRuntime runtime;
    /// <inheritdoc/>
    public string FuturesOptionContractId {get;}
    /// <inheritdoc/>
    public DateOnly ValueDate {get;}
    /// <summary>Binds copied contract and provider identities only.</summary>
    public NativeOwnedOptionPriceReader(NativeMarketDataRuntime runtime,string contract,DateOnly date,uint instrument,ushort publisher)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        if(instrument==0 || publisher==0) throw new ArgumentOutOfRangeException(nameof(instrument));
        this.runtime=runtime;FuturesOptionContractId=contract;ValueDate=date;
    }
    /// <summary>Binds only canonical identity; Rust resolves current provider
    /// bindings through every subsequent dataset generation.</summary>
    public NativeOwnedOptionPriceReader(NativeMarketDataRuntime runtime,string contract,DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(runtime);ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        this.runtime=runtime;FuturesOptionContractId=contract;ValueDate=date;
    }
    /// <inheritdoc/>
    public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot)
    {
        try {if(runtime.ReadDatasetOwnerPrices(MarketDataLane.OptionsGlbx,FuturesOptionContractId,ValueDate)?.Trade is {} trade){snapshot=trade.ToSnapshot();return true;}}
        catch(ObjectDisposedException){}
        snapshot=default;return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot)
    {
        try {if(runtime.ReadDatasetOwnerPrices(MarketDataLane.OptionsGlbx,FuturesOptionContractId,ValueDate)?.Quote is {} quote){snapshot=quote.ToSnapshot();return true;}}
        catch(ObjectDisposedException){}
        snapshot=default;return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastTradeWithGreeks(out LastTradeTickWithGreeksSnapshot snapshot)
    {
        try {
            var result=runtime.ReadDatasetOwnerTradeRisk(FuturesOptionContractId,ValueDate);
            if(result is {} value && value.Trade.ContractId==FuturesOptionContractId && value.Trade.ValueDate==ValueDate)
            {snapshot=new(value.Trade.ToSnapshot(),value.Greeks.ToSnapshot());return true;}
        }catch(ObjectDisposedException){}
        snapshot=default;return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastQuoteWithGreeks(out LastQuoteTickWithGreeksSnapshot snapshot)
    {
        try {
            var value=runtime.ReadDatasetOwnerQuoteRisk(FuturesOptionContractId,ValueDate);
            if(value is not null)
            {snapshot=new(value.Quote.ToSnapshot(),value.Greeks.ToSnapshot());return true;}
        }catch(ObjectDisposedException){}
        snapshot=default;return false;
    }
}
