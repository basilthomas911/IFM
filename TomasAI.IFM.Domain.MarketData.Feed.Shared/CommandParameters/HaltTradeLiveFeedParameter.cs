using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to halt a trade live feed.
/// </summary>
/// <param name="EntityId">The global Portfolio, Fund, Order, and Trade identity.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record HaltTradeLiveFeedParameter(TradeEntityId EntityId, int ErrorCode)
    : ICommandParameter;
