using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
public sealed class FinancialModelingPrepAuthenticationException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepAuthenticationException instance.</summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    public FinancialModelingPrepAuthenticationException(HttpStatusCode statusCode)
        : base($"FMP rejected the request credentials with HTTP status {(int)statusCode}.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
