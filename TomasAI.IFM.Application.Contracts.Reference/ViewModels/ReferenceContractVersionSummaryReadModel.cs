using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Describes one entry in a contract's bounded reference-version history.</summary>
public sealed record ReferenceContractVersionSummaryReadModel(string Version, bool Published);
