using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
public sealed class FinancialModelingPrepContractException : FinancialModelingPrepResponseException
{
    /// <summary>Initializes a new FinancialModelingPrepContractException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepContractException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new FinancialModelingPrepContractException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public FinancialModelingPrepContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
