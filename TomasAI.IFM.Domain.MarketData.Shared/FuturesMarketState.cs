namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Authoritative futures market and position-permission state in US Eastern time.</summary>
public enum FuturesMarketState
{
    Closed = 0,
    OffTrading = 1,
    LiveTrading = 2
}
