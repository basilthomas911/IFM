using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Captures one immutable, provider-neutral Regime Discovery market-signal snapshot.</summary>
public interface IRegimeDiscoveryMarketSignalSnapshotProvider
{
    /// <summary>Captures a revision-stable snapshot for the supplied exact requirements.</summary>
    /// <param name="request">Exact snapshot request.</param>
    /// <param name="cancellationToken">Signals cancellation.</param>
    /// <returns>A successful snapshot or explicit availability issues.</returns>
    ValueTask<RegimeDiscoveryMarketSignalSnapshotResult> CaptureAsync(
        RegimeDiscoveryMarketSignalSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
