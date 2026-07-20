using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to delete futures bar data.
/// </summary>
/// <param name="Id">The futures bar data identifier to delete.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record DeleteFuturesBarDataParameter(FuturesBarDataId Id, int ErrorCode)
    : ICommandParameter;
