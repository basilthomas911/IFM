using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to remove all trade live feeds for an order.
/// </summary>
/// <param name="OrderId">The unique order identifier.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record RemoveTradeLiveFeedsParameter(int OrderId, int ErrorCode)
    : ICommandParameter;
