using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
public sealed class FinancialModelingPrepRateLimitException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepRateLimitException instance.</summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    public FinancialModelingPrepRateLimitException(HttpStatusCode statusCode)
        : base($"FMP rate-limited the request with HTTP status {(int)statusCode} after bounded retries.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
