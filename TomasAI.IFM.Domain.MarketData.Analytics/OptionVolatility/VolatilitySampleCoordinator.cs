using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

/// <summary>Bounded in-memory cadence guard; durable history remains observation/snapshot based.</summary>
public sealed class VolatilitySampleCoordinator(TimeSpan coalescingInterval, int maximumIntradayCheckpointsPerValueDate)
{
    readonly object gate = new();
    readonly Dictionary<(string Series, string Version, DateOnly Date, string Slot), int> daily = new();
    readonly Dictionary<(string Series, string Version, DateOnly Date), SortedDictionary<long, int>> intraday = new();

    public VolatilitySampleDecision Admit(OptionIvObservation observation, VolatilitySampleKind kind)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (coalescingInterval <= TimeSpan.Zero || maximumIntradayCheckpointsPerValueDate <= 0)
            throw new InvalidOperationException("Sampling bounds must come from an approved series definition.");
        lock (gate)
        {
            if (kind == VolatilitySampleKind.DailyFinal)
            {
                var key = (observation.Series.SeriesId, observation.Series.MethodologyVersion,
                    observation.ExchangeValueDate, observation.SamplingSlot);
                if (daily.TryGetValue(key, out var revision) && revision >= observation.Revision)
                    return new(false, false, "DuplicateOrStaleDailyRevision");
                daily[key] = observation.Revision;
                return new(true, revision != 0, revision == 0 ? "DailyAccepted" : "DailyRevisionAccepted");
            }

            var seriesKey = (observation.Series.SeriesId, observation.Series.MethodologyVersion,
                observation.ExchangeValueDate);
            if (!intraday.TryGetValue(seriesKey, out var buckets))
                intraday[seriesKey] = buckets = new();
            var interval = checked((long)coalescingInterval.TotalMilliseconds);
            var bucket = observation.ObservedAtUtc.ToUnixTimeMilliseconds() / interval;
            if (buckets.TryGetValue(bucket, out var priorRevision))
            {
                if (priorRevision >= observation.Revision)
                    return new(false, false, "DuplicateOrStaleIntradayRevision");
                buckets[bucket] = observation.Revision;
                return new(true, true, "IntradayCheckpointCoalesced");
            }
            if (buckets.Count >= maximumIntradayCheckpointsPerValueDate)
                return new(false, false, "IntradayCheckpointLimitReached");
            buckets[bucket] = observation.Revision;
            return new(true, false, "IntradayCheckpointAccepted");
        }
    }
}
