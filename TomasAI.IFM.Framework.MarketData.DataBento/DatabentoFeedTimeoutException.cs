namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
public sealed class DatabentoFeedTimeoutException : TimeoutException
{
    /// <summary>Initializes a new DatabentoFeedTimeoutException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public DatabentoFeedTimeoutException(string message)
        : base(message)
    {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "This instance property implements command or exception metadata required by the published interface.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "This instance property implements command or exception metadata required by the published interface.")]
    public DatabentoFeedStatus Status => DatabentoFeedStatus.Timeout;
}
