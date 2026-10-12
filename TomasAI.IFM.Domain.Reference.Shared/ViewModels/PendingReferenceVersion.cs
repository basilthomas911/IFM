using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Identifies a staged immutable reference version awaiting publication.</summary>
public sealed record PendingReferenceVersion(string ContractId, string Version, string Digest);
