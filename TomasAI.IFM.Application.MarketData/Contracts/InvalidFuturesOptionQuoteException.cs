namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class InvalidFuturesOptionQuoteException : MarketDataContractException
{
    public InvalidFuturesOptionQuoteException(string futuresOptionContractId, string reason)
        : base(futuresOptionContractId,
            $"The latest quote for futures-option contract '{futuresOptionContractId}' is invalid: {reason}")
    {
    }
}
