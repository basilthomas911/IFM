using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Infrastructure-only epoch contract. A Rust facade factory supplies
/// native runtime ownership explicitly; business consumers use IMarketDataApi.</summary>
public interface IRustMarketDataEpoch : IDatabentoMarketDataEpoch
{
    /// <summary>The owned runtime for this API date; not a legacy worker process.</summary>
    NativeMarketDataRuntime Runtime { get; }
}

/// <summary>Creates exclusively native-backed epochs sharing the API business
/// clock. This distinct factory prevents accidental legacy provider composition.</summary>
public interface IRustMarketDataEpochFactory : IDatabentoMarketDataEpochFactory
{
    /// <summary>Authoritative API clock also used for application freshness/policy.</summary>
    TimeProvider Clock { get; }
    /// <summary>Creates a native-backed date-scoped owner without starting I/O.</summary>
    new IRustMarketDataEpoch Create(DateOnly valueDate);
    IDatabentoMarketDataEpoch IDatabentoMarketDataEpochFactory.Create(DateOnly valueDate) => Create(valueDate);
}
