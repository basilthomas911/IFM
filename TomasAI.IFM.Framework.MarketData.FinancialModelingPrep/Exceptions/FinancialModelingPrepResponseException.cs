using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
public class FinancialModelingPrepResponseException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepResponseException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepResponseException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new FinancialModelingPrepResponseException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public FinancialModelingPrepResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
