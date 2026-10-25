using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

/// <summary>A read must return the exact immutable reviewed mapping version; there is no latest fallback.</summary>
public interface IOptionPricingConventionStore
{
    Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken);
}
