namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class OnTheRunFuturesContractNotFoundException : MarketDataApiException
{
    public OnTheRunFuturesContractNotFoundException(string symbol, DateOnly valueDate)
        : base($"DataBento did not return an eligible currently traded futures contract for " +
               $"'{symbol}' at {valueDate:yyyy-MM-dd}.")
    {
        Symbol = symbol;
        ValueDate = valueDate;
    }

    public string Symbol { get; }
    public DateOnly ValueDate { get; }
}
