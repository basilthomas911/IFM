namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
public sealed class FeedStopDrainIncompleteException : DatabentoFeedException
{
    /// <summary>Initializes a new FeedStopDrainIncompleteException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FeedStopDrainIncompleteException(string message)
        : base(DatabentoFeedStatus.StopDrainIncomplete, message)
    {
    }
}
