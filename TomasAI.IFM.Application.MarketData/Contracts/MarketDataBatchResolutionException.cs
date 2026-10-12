namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class MarketDataBatchResolutionException : MarketDataApiException
{
    public MarketDataBatchResolutionException(IReadOnlyList<string> unresolvedContractIds)
        : base($"The market-data batch could not resolve: {string.Join(", ", unresolvedContractIds)}.")
    {
        UnresolvedContractIds = unresolvedContractIds.ToArray();
    }

    public IReadOnlyList<string> UnresolvedContractIds { get; }
}
