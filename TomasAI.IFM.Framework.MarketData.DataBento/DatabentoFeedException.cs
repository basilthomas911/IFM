namespace TomasAI.IFM.Framework.MarketData.DataBento;

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

public sealed class DatabentoFeedTimeoutException : TimeoutException
{
    /// <summary>Initializes a new DatabentoFeedTimeoutException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public DatabentoFeedTimeoutException(string message)
        : base(message)
    {
    }

    public DatabentoFeedStatus Status => DatabentoFeedStatus.Timeout;
}

public sealed class FeedStopDrainIncompleteException : DatabentoFeedException
{
    /// <summary>Initializes a new FeedStopDrainIncompleteException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FeedStopDrainIncompleteException(string message)
        : base(DatabentoFeedStatus.StopDrainIncomplete, message)
    {
    }
}

public enum ContractMappingDirection
{
    ContractIdToInstrumentId = 1,
    InstrumentIdToContractId = 2
}

public sealed class DatabentoContractMappingException : DatabentoFeedException
{
    /// <summary>Initializes a new DatabentoContractMappingException instance.</summary>
    /// <param name="direction">The mapping direction that failed.</param>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <param name="instrumentId">The provider instrument identifier.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public DatabentoContractMappingException(
        ContractMappingDirection direction,
        string message,
        string? contractId = null,
        uint? instrumentId = null,
        Exception? innerException = null)
        : base(
            DatabentoFeedStatus.SymbolResolutionFailed,
            message,
            innerException)
    {
        Direction = direction;
        ContractId = contractId;
        InstrumentId = instrumentId;
    }

    public ContractMappingDirection Direction { get; }
    public string? ContractId { get; }
    public uint? InstrumentId { get; }
}
