using System.Collections.Frozen;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.OptionChainCache;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Background-only ownership of prepared cache publications; never exposed through IMarketDataApi.</summary>
public interface IOptionChainSnapshotPublisher
{
    /// <summary>Starts a fresh admitted generation.</summary>
    void Admit(Guid generation);
    /// <summary>Invalidates quotes for the failing generation.</summary>
    void Fence(Guid generation);
    /// <summary>Removes a retired policy scope without stopping unrelated subscription owners.</summary>
    void Retire(Guid generation, string lookupScope);
    /// <summary>Publishes an immutable snapshot already priced and validated by background maintenance.</summary>
    bool Publish(MarketCompositionSnapshot snapshot, DateOnly valueDate, string configurationDigest, long version, string? lookupScope = null);
}
