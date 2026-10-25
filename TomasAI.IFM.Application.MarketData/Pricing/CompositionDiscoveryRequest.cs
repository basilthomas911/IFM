using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record CompositionDiscoveryRequest(Guid LeaseId, Guid GenerationId, DateOnly ValueDate,
    DateOnly MaturityDate, DateTimeOffset DeadlineUtc, IReadOnlyList<OptionDefinitionCandidate> Definitions,
    bool ScopeComplete, OptionPricingCalendar Calendar, TreasuryPublicationPolicy Publication,
    TreasuryRateConversionPolicy Conversion, bool SeparateContractConnection = false, bool IndependentChainConnection = false);
