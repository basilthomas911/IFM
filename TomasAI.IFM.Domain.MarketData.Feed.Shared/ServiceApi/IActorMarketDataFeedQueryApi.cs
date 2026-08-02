namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

/// <summary>
/// Defines Market Data Feed queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorMarketDataFeedQueryApi : IMarketDataFeedQueryApi;
