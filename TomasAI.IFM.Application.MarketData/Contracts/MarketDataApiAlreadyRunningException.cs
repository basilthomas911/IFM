namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
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
