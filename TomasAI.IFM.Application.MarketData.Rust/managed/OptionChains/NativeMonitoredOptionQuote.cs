using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

namespace TomasAI.IFM.Application.MarketData.Rust.OptionChains;

/// <summary>One immutable monitoring binding. Its predicate fences a retired
/// lease/context; its reader opens no subscription and owns no provider task.</summary>
public sealed class NativeMonitoredOptionQuote
{
    /// <summary>Qualified native physical scope name.</summary>
    public string ScopeId {get;}
    /// <summary>Reviewed expiry date used by the existing observation contract.</summary>
    public DateOnly MaturityDate {get;}
    /// <summary>Exact immutable application reference context for this binding.</summary>
    public PreparedOptionPricingContext Prepared {get;}
    /// <summary>Non-consuming current-generation source/pricing reader.</summary>
    public NativeOptionPriceReader Reader {get;}
    /// <summary>Nonblocking application lifetime and revision check.</summary>
    public Func<bool> IsCurrent {get;}
    internal NativeMonitoredOptionQuote(string scope,DateOnly maturity,PreparedOptionPricingContext prepared,NativeOptionPriceReader reader,Func<bool> isCurrent)
    {ScopeId=scope;MaturityDate=maturity;Prepared=prepared;Reader=reader;IsCurrent=isCurrent;}
}
