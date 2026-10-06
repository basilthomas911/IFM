using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

/// <summary>Immutable proposed business change; authoritative contract state remains in the State object.</summary>
/// <param name="FuturesOptionContractId">The proposed FuturesOptionContractId.</param>
/// <param name="Accepted">Whether the current state permits this change.</param>
internal readonly record struct FuturesOptionContractRemoval(
    string FuturesOptionContractId,
    bool Accepted);
