namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

/// <summary>
/// Defines Market Data queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorMarketDataQueryApi : IMarketDataQueryApi;
