using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert futures tick data.
/// </summary>
/// <param name="FuturesContract">The futures contract metadata associated with the tick data.</param>
/// <param name="FuturesTickData">The futures tick data to insert.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertFuturesTickDataParameter(FuturesContractV2ReadModel FuturesContract, FuturesTickDataV2ReadModel FuturesTickData, int ErrorCode)
    : ICommandParameter;
