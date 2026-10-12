namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class MarketDataContractMappingException : MarketDataContractException
{
    public MarketDataContractMappingException(string contractId, string reason)
        : base(contractId, $"Market-data mapping for '{contractId}' is invalid: {reason}")
    {
    }
}
