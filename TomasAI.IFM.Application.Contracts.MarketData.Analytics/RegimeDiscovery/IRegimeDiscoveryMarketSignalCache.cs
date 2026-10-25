using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Accepts immutable normalized analytics observations into the latest-signal cache.</summary>
public interface IRegimeDiscoveryMarketSignalCache
{
    /// <summary>Gets the latest monotonic cache revision.</summary>
    long Revision { get; }
    /// <summary>Publishes one immutable normalized observation and advances the cache revision.</summary>
    void Upsert(RegimeDiscoverySignalObservation observation);
    /// <summary>Clears process-local observations during controlled restart or tests.</summary>
    void Clear();
}
