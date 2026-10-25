using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record CompositionDiscoveryResult(WorkerOptionChainRequest? Lease,
    ImmutableArray<OptionPricingFailure> Exclusions, bool CompleteEmpty, OptionPricingFailure? Failure);
