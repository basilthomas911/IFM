using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
public sealed class FinancialModelingPrepResponseTooLargeException : FinancialModelingPrepResponseException
{
    /// <summary>Initializes a new FinancialModelingPrepResponseTooLargeException instance.</summary>
    /// <param name="maximumBytes">The maximum permitted response size in bytes.</param>
    public FinancialModelingPrepResponseTooLargeException(int maximumBytes)
        : base($"The FMP response exceeded the configured {maximumBytes}-byte limit.")
    {
    }
}
