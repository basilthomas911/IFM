using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Immutable reference data and its reviewed pricing convention.</summary>
[MessagePackObject]
public sealed record ReferenceContractVersion(
    [property: Key(0)] FuturesContractV3ReadModel? Future,
    [property: Key(1)] FuturesOptionContractReadModel? Option,
    [property: Key(2)] OptionPricingConvention? Convention);

/// <summary>Identifies a staged immutable reference version awaiting publication.</summary>
public sealed record PendingReferenceVersion(string ContractId, string Version, string Digest);

/// <summary>Describes one entry in a contract's bounded reference-version history.</summary>
public sealed record ReferenceContractVersionSummaryReadModel(string Version, bool Published);
