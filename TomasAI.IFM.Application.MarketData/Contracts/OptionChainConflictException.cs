namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
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
