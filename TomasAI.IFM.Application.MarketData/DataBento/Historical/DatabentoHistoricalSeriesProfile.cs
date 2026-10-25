using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>Configures one provider profile for a domain market series.</summary>
public sealed record DatabentoHistoricalSeriesProfile
{
    /// <summary>Gets the exact formatted <c>MarketSeriesIdentity</c>.</summary>
    public required string MarketSeriesIdentity { get; init; }
    /// <summary>Gets the Databento dataset.</summary>
    public required string Dataset { get; init; }
    /// <summary>Gets the Databento symbols.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serializer contract requires its existing array shape.")]
    public required string[] Symbols { get; init; }
    /// <summary>Gets the provider input symbology.</summary>
    public HistoricalSymbology Symbology { get; init; } = HistoricalSymbology.RawSymbol;
    /// <summary>Gets the exact domain contract used for fixed-contract profiles.</summary>
    public string ContractId { get; init; } = string.Empty;
}
