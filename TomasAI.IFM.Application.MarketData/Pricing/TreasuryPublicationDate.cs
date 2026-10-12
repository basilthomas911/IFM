using System.Collections.Immutable;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>One reviewed expected publication deadline, including the configured provider allowance.</summary>
public sealed record TreasuryPublicationDate(DateOnly ValueDate, DateTimeOffset AvailableByUtc);
