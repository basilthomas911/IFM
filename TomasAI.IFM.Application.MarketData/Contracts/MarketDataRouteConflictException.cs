namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
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
