namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>
/// Base type for failures reported by the application market-data boundary.
/// </summary>
public abstract class MarketDataApiException : Exception
{
    protected MarketDataApiException(string message)
        : base(message)
    {
    }

    protected MarketDataApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class MarketDataApiNotRunningException : MarketDataApiException
{
    public MarketDataApiNotRunningException()
        : base("The market-data API does not have a running value-date epoch.")
    {
    }
}

public sealed class FuturesContractRolloverConfigurationException : MarketDataApiException
{
    public FuturesContractRolloverConfigurationException(string message)
        : base(message)
    {
    }
}

public sealed class CurrentlyTradedFuturesContractNotFoundException : MarketDataApiException
{
    public CurrentlyTradedFuturesContractNotFoundException(string symbol, DateOnly valueDate)
        : base($"DataBento did not return an eligible currently traded futures contract for " +
               $"'{symbol}' at {valueDate:yyyy-MM-dd}.")
    {
        Symbol = symbol;
        ValueDate = valueDate;
    }

    public string Symbol { get; }
    public DateOnly ValueDate { get; }
}

public sealed class MarketDataApiAlreadyRunningException : MarketDataApiException
{
    public MarketDataApiAlreadyRunningException(
        DateOnly activeValueDate,
        DateOnly requestedValueDate)
        : base($"The market-data API is already running for {activeValueDate:yyyy-MM-dd}; " +
               $"stop it before starting {requestedValueDate:yyyy-MM-dd}.")
    {
        ActiveValueDate = activeValueDate;
        RequestedValueDate = requestedValueDate;
    }

    public DateOnly ActiveValueDate { get; }
    public DateOnly RequestedValueDate { get; }
}

public sealed class MarketDataApiValueDateMismatchException : MarketDataApiException
{
    public MarketDataApiValueDateMismatchException(
        DateOnly activeValueDate,
        DateOnly requestedValueDate)
        : base($"The requested value date {requestedValueDate:yyyy-MM-dd} does not match " +
               $"the active market-data epoch {activeValueDate:yyyy-MM-dd}.")
    {
        ActiveValueDate = activeValueDate;
        RequestedValueDate = requestedValueDate;
    }

    public DateOnly ActiveValueDate { get; }
    public DateOnly RequestedValueDate { get; }
}

public abstract class MarketDataContractException : MarketDataApiException
{
    protected MarketDataContractException(string contractId, string message)
        : base(message)
    {
        ContractId = contractId;
    }

    public string ContractId { get; }
}

public sealed class MarketDataContractNotFoundException : MarketDataContractException
{
    public MarketDataContractNotFoundException(string contractId)
        : base(contractId, $"Market-data contract '{contractId}' is not configured in the active epoch.")
    {
    }
}

public sealed class MarketDataContractKindMismatchException : MarketDataContractException
{
    public MarketDataContractKindMismatchException(
        string contractId,
        string expectedKind,
        string actualKind)
        : base(contractId, $"Market-data contract '{contractId}' is '{actualKind}', not '{expectedKind}'.")
    {
        ExpectedKind = expectedKind;
        ActualKind = actualKind;
    }

    public string ExpectedKind { get; }
    public string ActualKind { get; }
}

public sealed class MarketDataBatchResolutionException : MarketDataApiException
{
    public MarketDataBatchResolutionException(IReadOnlyList<string> unresolvedContractIds)
        : base($"The market-data batch could not resolve: {string.Join(", ", unresolvedContractIds)}.")
    {
        UnresolvedContractIds = unresolvedContractIds.ToArray();
    }

    public IReadOnlyList<string> UnresolvedContractIds { get; }
}

public sealed class MarketDataContractMappingException : MarketDataContractException
{
    public MarketDataContractMappingException(string contractId, string reason)
        : base(contractId, $"Market-data mapping for '{contractId}' is invalid: {reason}")
    {
    }
}

public sealed class FuturesLastPriceUnavailableException : MarketDataContractException
{
    public FuturesLastPriceUnavailableException(string futuresContractId)
        : base(futuresContractId,
            $"No fresh last-trade price is available for futures contract '{futuresContractId}'.")
    {
    }
}

public sealed class InvalidFuturesOptionQuoteException : MarketDataContractException
{
    public InvalidFuturesOptionQuoteException(string futuresOptionContractId, string reason)
        : base(futuresOptionContractId,
            $"The latest quote for futures-option contract '{futuresOptionContractId}' is invalid: {reason}")
    {
    }
}

public sealed class TickAggregationNotRunningException : MarketDataContractException
{
    public TickAggregationNotRunningException(string futuresContractId)
        : base(futuresContractId,
            $"Tick aggregation is not running for underlying futures contract '{futuresContractId}'.")
    {
    }
}

public sealed class UnderlyingTickerNotRunningException : MarketDataContractException
{
    public UnderlyingTickerNotRunningException(string futuresContractId)
        : base(futuresContractId,
            $"The underlying futures ticker '{futuresContractId}' is not configured and running.")
    {
    }
}

public sealed class MarketDataRouteConflictException : MarketDataContractException
{
    public MarketDataRouteConflictException(string futuresOptionContractId, string existingOwner)
        : base(futuresOptionContractId,
            $"Futures-option contract '{futuresOptionContractId}' is already owned by route '{existingOwner}'.")
    {
        ExistingOwner = existingOwner;
    }

    public string ExistingOwner { get; }
}

public sealed class OptionChainConflictException : MarketDataApiException
{
    public OptionChainConflictException(string futuresContractId, DateOnly maturityDate)
        : base($"A different immutable option-chain selection is already running for " +
               $"'{futuresContractId}' at {maturityDate:yyyy-MM-dd}.")
    {
        FuturesContractId = futuresContractId;
        MaturityDate = maturityDate;
    }

    public string FuturesContractId { get; }
    public DateOnly MaturityDate { get; }
}

public sealed class MarketDataCapacityExceededException : MarketDataApiException
{
    public MarketDataCapacityExceededException(string resourceName, int capacity)
        : base($"Market-data resource '{resourceName}' reached its configured capacity of {capacity}.")
    {
        ResourceName = resourceName;
        Capacity = capacity;
    }

    public string ResourceName { get; }
    public int Capacity { get; }
}

public sealed class MarketDataPricingInputUnavailableException : MarketDataApiException
{
    public MarketDataPricingInputUnavailableException(string inputName)
        : base($"Required market-data pricing input '{inputName}' is unavailable.")
    {
        InputName = inputName;
    }

    public string InputName { get; }
}
