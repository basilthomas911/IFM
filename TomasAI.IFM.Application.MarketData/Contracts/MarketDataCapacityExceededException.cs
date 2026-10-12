namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
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
