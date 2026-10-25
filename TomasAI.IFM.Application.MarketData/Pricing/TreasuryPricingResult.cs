using System.Collections.Immutable;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Validated daily observation and its publication-validity boundary.</summary>
public sealed record TreasuryPricingResult(TreasuryContinuousRate? Rate, DateTimeOffset ValidUntilUtc, string? Error)
{
    public bool Succeeded => Rate is not null && Error is null;
}
