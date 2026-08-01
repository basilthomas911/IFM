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

public enum ContractMappingDirection
{
    ContractIdToInstrumentId = 1,
    InstrumentIdToContractId = 2
}

public sealed class DatabentoContractMappingException : DatabentoFeedException
{
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
