namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>
/// Base type for failures reported by the application market-data boundary.
/// </summary>
public abstract class MarketDataApiException : Exception
{
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    protected MarketDataApiException() : base("MarketDataApiException") { }
    protected MarketDataApiException(string message)
        : base(message)
    {
    }

    protected MarketDataApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
