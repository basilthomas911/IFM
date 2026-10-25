using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Immutable reference data and its reviewed pricing convention.</summary>
[MessagePackObject]
public sealed record ReferenceContractVersion(
    [property: Key(0)] FuturesContractV3ReadModel? Future,
    [property: Key(1)] FuturesOptionContractReadModel? Option,
    [property: Key(2)] OptionPricingConvention? Convention);
