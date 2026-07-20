using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to enable a trade live feed.
/// </summary>
/// <param name="OrderId">The unique order identifier.</param>
/// <param name="TradeId">The unique trade identifier within the order.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record EnableTradeLiveFeedParameter(int OrderId, int TradeId, int ErrorCode)
    : ICommandParameter;
