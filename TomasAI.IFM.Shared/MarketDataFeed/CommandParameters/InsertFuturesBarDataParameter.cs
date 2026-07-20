using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert futures bar data.
/// </summary>
/// <param name="FuturesBarData">The futures bar data to insert.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertFuturesBarDataParameter(FuturesBarDataReadModel FuturesBarData, int ErrorCode)
    : ICommandParameter;
