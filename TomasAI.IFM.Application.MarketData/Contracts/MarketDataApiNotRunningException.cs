namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed class MarketDataApiNotRunningException : MarketDataApiException
{
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    public MarketDataApiNotRunningException(string message) : base(message) { }
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    public MarketDataApiNotRunningException(string message, Exception innerException) : base(message, innerException) { }
    public MarketDataApiNotRunningException()
        : base("The market-data API does not have a running value-date epoch.")
    {
    }
}
