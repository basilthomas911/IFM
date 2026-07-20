using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert a futures closing price.
/// </summary>
/// <param name="Id">The futures data identifier.</param>
/// <param name="ClosingPrice">The closing price to insert.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertFuturesClosingPriceParameter(FuturesDataId Id, decimal ClosingPrice, int ErrorCode)
    : ICommandParameter;
