using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert futures option tick data.
/// </summary>
/// <param name="FuturesContract">The futures contract metadata associated with the option tick data.</param>
/// <param name="FuturesOptionTickData">The futures option tick data to insert.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertFuturesOptionTickDataParameter(FuturesContractV2ReadModel FuturesContract, FuturesOptionTickDataV2ReadModel FuturesOptionTickData, int ErrorCode)
    : ICommandParameter;
