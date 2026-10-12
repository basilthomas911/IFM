namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
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
