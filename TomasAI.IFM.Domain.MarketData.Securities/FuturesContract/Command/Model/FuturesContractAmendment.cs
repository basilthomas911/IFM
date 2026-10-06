using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;

/// <summary>Immutable proposed business change; authoritative contract state remains in the State object.</summary>
/// <param name="OriginalFuturesContractId">The proposed OriginalFuturesContractId.</param>
/// <param name="FuturesContract">The proposed FuturesContract.</param>
/// <param name="Accepted">Whether the current state permits this change.</param>
internal readonly record struct FuturesContractAmendment(
    FuturesContractId OriginalFuturesContractId,
    FuturesContractV3ReadModel FuturesContract,
    bool Accepted);
