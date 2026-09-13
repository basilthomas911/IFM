using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to remove a trade live feed.
/// </summary>
/// <param name="EntityId">The global Portfolio, Fund, Order, and Trade identity.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record RemoveTradeLiveFeedParameter(TradeEntityId EntityId, DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
