namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
public sealed class DatabentoRecoveryException : Exception
{
    /// <summary>Initializes a new DatabentoRecoveryException instance.</summary>
    /// <param name="fault">The feed fault that triggered recovery.</param>
    /// <param name="attempts">The number of recovery attempts performed.</param>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public DatabentoRecoveryException(
        FeedRecoveryFaultKind fault,
        int attempts,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Fault = fault;
        Attempts = attempts;
    }

    public FeedRecoveryFaultKind Fault { get; }
    public int Attempts { get; }
}
