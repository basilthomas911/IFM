using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

/// <summary>Immutable proposed business change; authoritative contract state remains in the State object.</summary>
/// <param name="FuturesOptionContracts">The proposed FuturesOptionContracts.</param>
internal readonly record struct FuturesOptionContractBatchAddition(
    FuturesOptionContractReadModel[] FuturesOptionContracts);
