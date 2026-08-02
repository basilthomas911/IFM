namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

/// <summary>
/// Defines Market Data Analytics queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorMarketDataAnalyticsQueryApi : IMarketDataAnalyticsQueryApi;
