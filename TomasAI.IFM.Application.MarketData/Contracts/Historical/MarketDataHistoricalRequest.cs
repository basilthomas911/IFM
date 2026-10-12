using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Describes one cost-controlled application historical acquisition.</summary>
public sealed record MarketDataHistoricalRequest
{
    /// <summary>Gets the stable data load attempt identity.</summary>
    public required Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the requested series profiles.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serializer contract requires its existing array shape.")]
    public required MarketDataHistoricalSeriesRequest[] Series { get; init; }
    /// <summary>Gets the inclusive first futures value date.</summary>
    public required DateOnly StartDate { get; init; }
    /// <summary>Gets the inclusive last futures value date.</summary>
    public required DateOnly EndDate { get; init; }
    /// <summary>Gets the maximum approved provider cost in US dollars.</summary>
    public required decimal MaximumCostUsd { get; init; }
    /// <summary>Gets the maximum approved provider bytes.</summary>
    public required long MaximumBytes { get; init; }
    /// <summary>Gets an approved override identity when normal limits may be exceeded.</summary>
    public string ApprovedOverrideId { get; init; } = string.Empty;
    /// <summary>Gets the normalization implementation version.</summary>
    public required string NormalizationVersion { get; init; }
    /// <summary>Gets the requesting principal.</summary>
    public required string RequestedBy { get; init; }
    /// <summary>Gets the active contract that receives a continuation-series Analytics result.</summary>
    public string AnalyticsTargetContractId { get; init; } = string.Empty;
}
