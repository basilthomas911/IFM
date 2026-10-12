using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Describes one cost-controlled provider historical request.</summary>
public sealed record HistoricalProviderRequest
{
    /// <summary>Gets the provider dataset.</summary>
    public required string Dataset { get; init; }
    /// <summary>Gets the requested provider symbols.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserves the existing array-shaped provider snapshot contract.")]
    public required string[] Symbols { get; init; }
    /// <summary>Gets the requested schema.</summary>
    public required HistoricalDataSchema Schema { get; init; }
    /// <summary>Gets the input symbology.</summary>
    public required HistoricalSymbology Symbology { get; init; }
    /// <summary>Gets the inclusive UTC range start.</summary>
    public required DateTimeOffset StartUtc { get; init; }
    /// <summary>Gets the exclusive UTC range end.</summary>
    public required DateTimeOffset EndUtc { get; init; }
    /// <summary>Gets the maximum provider records, or zero for the approved complete range.</summary>
    public long RecordLimit { get; init; }
    /// <summary>Gets the stable request hash used for idempotency.</summary>
    public required string RequestHash { get; init; }
}
