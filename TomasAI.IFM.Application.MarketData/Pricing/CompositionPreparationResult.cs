using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record CompositionPreparationResult(CompositionPreparation? Preparation, OptionPricingFailure? Failure);
