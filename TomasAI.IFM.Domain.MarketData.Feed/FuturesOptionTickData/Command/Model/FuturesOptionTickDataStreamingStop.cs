using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;

/// <summary>Immutable business inputs for StopFuturesOptionTickDataStreaming; contains no state mutation or external effects.</summary>
/// <param name="ContractId">The proposed contract id.</param>
internal readonly record struct FuturesOptionTickDataStreamingStop(
    string ContractId);
