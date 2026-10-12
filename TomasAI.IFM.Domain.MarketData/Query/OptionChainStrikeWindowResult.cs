using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>The selected cached contracts and the actual selected strike boundaries.</summary>
/// <param name="Contracts">Cached contracts selected for the chain.</param>
/// <param name="LowerBound">Lowest selected strike, excluding only required legs outside the window.</param>
/// <param name="UpperBound">Highest selected strike, excluding only required legs outside the window.</param>
/// <param name="Method">Selection method, including whether the IV window was clipped.</param>
#pragma warning disable CA1819 // Preserve the published snapshot array serialization contract.
public sealed record OptionChainStrikeWindowResult(
    FuturesOptionContractReadModel[] Contracts, decimal? LowerBound, decimal? UpperBound, string Method);

#pragma warning restore CA1819
