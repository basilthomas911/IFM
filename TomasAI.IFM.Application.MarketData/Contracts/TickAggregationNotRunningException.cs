namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class TickAggregationNotRunningException : MarketDataContractException
{
    public TickAggregationNotRunningException(string futuresContractId)
        : base(futuresContractId,
            $"Tick aggregation is not running for underlying futures contract '{futuresContractId}'.")
    {
    }
}
