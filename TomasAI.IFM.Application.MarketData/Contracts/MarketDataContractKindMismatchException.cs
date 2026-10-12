namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
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
