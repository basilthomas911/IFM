using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>Assembles exact persisted Analytics context around one raw Daily EOD session.</summary>
public sealed class FuturesEodAnalyticsAssembler(
    IHistoricalObservationStore observationStore,
    IHistoricalAnalyticsSignalReader signalReader)
    : IFuturesEodAnalyticsAssembler
{
    /// <inheritdoc />
    public async ValueTask<FuturesEodAnalyticsAssembly?> AssembleAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        IReadOnlyCollection<MarketAnalyticsSignalFamily> requestedSignals,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestedSignals);
        var raw = await observationStore.GetRawEodAsync(
            seriesIdentity, valueDate, cancellationToken).ConfigureAwait(false);
        if (raw is null) return null;
        var values = new Dictionary<MarketAnalyticsSignalFamily, MarketAnalyticsSignalResult>();
        var missing = new List<MarketAnalyticsSignalFamily>();
        foreach (var family in requestedSignals.Distinct())
        {
            var value = await signalReader.GetExactAsync(
                family, seriesIdentity, TimeFrameType.Daily,
                raw.ObservationId, cancellationToken).ConfigureAwait(false);
            if (value is null)
            {
                missing.Add(family);
                continue;
            }
            if (value.Metadata.ObservationId != raw.ObservationId)
                throw new InvalidDataException($"{family} did not match raw ObservationId {raw.ObservationId}.");
            values.Add(family, value);
        }
        return new(raw, values, missing);
    }
}
