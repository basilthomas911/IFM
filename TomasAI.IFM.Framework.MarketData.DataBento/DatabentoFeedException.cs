namespace TomasAI.IFM.Framework.MarketData.DataBento;

public class DatabentoFeedException : Exception
{
    public DatabentoFeedException(
        DatabentoFeedStatus status,
        string message)
        : base(message)
    {
        Status = status;
    }

    public DatabentoFeedStatus Status { get; }
}

public sealed class DatabentoFeedTimeoutException : TimeoutException
{
    public DatabentoFeedTimeoutException(string message)
        : base(message)
    {
    }

    public DatabentoFeedStatus Status => DatabentoFeedStatus.Timeout;
}

public sealed class FeedStopDrainIncompleteException : DatabentoFeedException
{
    public FeedStopDrainIncompleteException(string message)
        : base(DatabentoFeedStatus.StopDrainIncomplete, message)
    {
    }
}
