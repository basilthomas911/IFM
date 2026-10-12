using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires diagnostic fields and a specific outcome; generic constructors would permit incomplete operational evidence.")]
public sealed class FinancialModelingPrepConfigurationException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepConfigurationException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepConfigurationException(string message)
        : base(message)
    {
    }
}
