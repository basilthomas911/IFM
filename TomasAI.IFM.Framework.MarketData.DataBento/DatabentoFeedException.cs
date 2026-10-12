namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
public class DatabentoFeedException : Exception
{
    /// <summary>Initializes a new DatabentoFeedException instance.</summary>
    /// <param name="status">The native provider result status.</param>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public DatabentoFeedException(
        DatabentoFeedStatus status,
        string message)
        : base(message)
    {
        Status = status;
    }

    /// <summary>Initializes a new DatabentoFeedException instance.</summary>
    /// <param name="status">The native provider result status.</param>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public DatabentoFeedException(
        DatabentoFeedStatus status,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        Status = status;
    }

    public DatabentoFeedStatus Status { get; }
}
