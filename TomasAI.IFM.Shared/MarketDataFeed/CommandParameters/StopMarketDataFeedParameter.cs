using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to stop a market data feed.
/// </summary>
/// <param name="ValueDate">The value (trading) date for which the market data feed should be stopped.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopMarketDataFeedParameter(DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
