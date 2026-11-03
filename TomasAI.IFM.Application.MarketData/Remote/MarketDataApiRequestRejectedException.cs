using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>A request rejected before invoking the backend; it does not indicate dataset processing failure.</summary>
public sealed class MarketDataApiRequestRejectedException : MarketDataApiException
{
    public MarketDataApiRequestRejectedException() : base("Market-data API request was rejected.") { }
    public MarketDataApiRequestRejectedException(string message) : base(message) { }
    public MarketDataApiRequestRejectedException(string message, Exception innerException) : base(message, innerException) { }
}
