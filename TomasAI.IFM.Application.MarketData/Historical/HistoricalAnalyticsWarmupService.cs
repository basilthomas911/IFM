using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>
/// Ensures Development-only historical coverage and replays stored Daily observations in order.
/// </summary>
public sealed class HistoricalAnalyticsWarmupService
{
    readonly HistoricalAnalyticsWarmupOptions options;
    readonly HistoricalDataLoader loader;
    readonly IHistoricalObservationStore observationStore;
    readonly IHistoricalDailyReplayPublisher dailyReplayPublisher;
    readonly IMarketSessionCalendar calendar;
    readonly TimeProvider timeProvider;
    readonly SemaphoreSlim gate = new(1, 1);
    string lastCompletedReplayKey = string.Empty;

    public HistoricalAnalyticsWarmupService(
        HistoricalAnalyticsWarmupOptions options,
        HistoricalDataLoader loader,
        IHistoricalObservationStore observationStore,
        IHistoricalDailyReplayPublisher dailyReplayPublisher,
        IMarketSessionCalendar calendar,
        TimeProvider timeProvider)
    {
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        this.observationStore = observationStore ?? throw new ArgumentNullException(nameof(observationStore));
        this.dailyReplayPublisher = dailyReplayPublisher ?? throw new ArgumentNullException(nameof(dailyReplayPublisher));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<HistoricalAnalyticsWarmupResult> EnsureAsync(
        MarketDataHistoricalRequest template,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!options.IsDevelopmentEnvironment)
            return new(HistoricalAnalyticsWarmupOutcome.IgnoredInProduction, default, default, 0, 0, null);
        if (!options.Enabled)
            return new(HistoricalAnalyticsWarmupOutcome.Disabled, default, default, 0, 0, null);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var endDate = LastCompletedTradingDate(template.EndDate);
            var startDate = endDate.AddDays(-(options.LookbackCalendarDays - 1));
            var expected = TradingDates(startDate, endDate);
            var trailingGraceDates = expected
                .TakeLast(options.TrailingProviderAvailabilityGraceSessions)
                .ToHashSet();
            var coverage = await ReadCoverageAsync(template.Series, startDate, endDate, cancellationToken)
                .ConfigureAwait(false);
            var missingBySeries = template.Series
                .Select(series => new
                {
                    Series = series,
                    Missing = MissingTradingDates(
                        coverage[series.SeriesIdentity],
                        expected,
                        trailingGraceDates,
                        options.MinimumValidDailySessions)
                })
                .Where(value => value.Missing.Length > 0)
                .ToArray();

            HistoricalDataLoaderState? loaded = null;
            if (missingBySeries.Length > 0)
            {
                foreach (var missing in missingBySeries)
                {
                    foreach (var range in ContiguousRanges(missing.Missing, expected))
                    {
                        loaded = await loader.ExecuteAsync(template with
                        {
                            DataLoadAttemptId = AttemptId(
                                missing.Series.SeriesIdentity,
                                range.StartDate,
                                range.EndDate,
                                options.NormalizationVersion),
                            Series = [missing.Series],
                            StartDate = range.StartDate,
                            EndDate = range.EndDate,
                            MaximumCostUsd = options.MaximumCostUsd,
                            MaximumBytes = options.MaximumBytes,
                            NormalizationVersion = options.NormalizationVersion
                        }, cancellationToken).ConfigureAwait(false);
                    }
                }
                coverage = await ReadCoverageAsync(template.Series, startDate, endDate, cancellationToken)
                    .ConfigureAwait(false);
            }

            var remainingBlockingMissing = template.Series.Sum(series => MissingTradingDates(
                coverage[series.SeriesIdentity],
                expected,
                trailingGraceDates,
                options.MinimumValidDailySessions).Length);
            if (remainingBlockingMissing > 0)
                throw new InvalidDataException(
                    $"Historical coverage remains incomplete after acquisition: {remainingBlockingMissing} non-grace trading sessions are missing or invalid.");

            var remainingMissing = template.Series.Sum(series => expected.Count(date =>
                !coverage[series.SeriesIdentity].Any(value =>
                    value.ValueDate == date && value.IsComplete && value.IsValid)));

            var ordered = coverage
                .OrderBy(static pair => pair.Key.Format(), StringComparer.Ordinal)
                .SelectMany(static pair => pair.Value
                    .Where(static value => value.IsComplete && value.IsValid)
                    .GroupBy(static value => value.ValueDate)
                    .Select(static group => group.OrderByDescending(value => value.LastMarketEventUtc).First())
                    .OrderBy(static value => value.ValueDate))
                .ToArray();
            var esCount = coverage
                .Where(static pair => pair.Key.FuturesSeriesId is { } series
                    && series.RootSymbol.Equals("ES", StringComparison.OrdinalIgnoreCase))
                .SelectMany(static pair => pair.Value)
                .Where(static value => value.IsComplete && value.IsValid)
                .Select(static value => value.ValueDate)
                .Distinct()
                .Count();
            if (esCount < options.MinimumValidDailySessions)
                throw new InvalidDataException(
                    $"ES historical coverage has {esCount} valid Daily sessions; {options.MinimumValidDailySessions} are required.");

            var replayKey = ReplayKey(startDate, endDate, ordered);
            var replayed = !string.Equals(lastCompletedReplayKey, replayKey, StringComparison.Ordinal);
            if (replayed)
            {
                await dailyReplayPublisher.PublishAsync(
                    ordered,
                    template.EndDate,
                    template.AnalyticsTargetContractId,
                    cancellationToken).ConfigureAwait(false);
                lastCompletedReplayKey = replayKey;
            }

            return new(
                missingBySeries.Length > 0
                    ? HistoricalAnalyticsWarmupOutcome.AcquiredAndReplayed
                    : replayed
                        ? HistoricalAnalyticsWarmupOutcome.ReplayedFromStorage
                        : HistoricalAnalyticsWarmupOutcome.AlreadyCurrent,
                startDate,
                endDate,
                esCount,
                remainingMissing,
                loaded);
        }
        finally
        {
            gate.Release();
        }
    }

    readonly record struct AcquisitionRange(DateOnly StartDate, DateOnly EndDate);

    static DateOnly[] MissingTradingDates(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        IReadOnlyList<DateOnly> expected,
        IReadOnlySet<DateOnly> trailingGraceDates,
        int minimumValidDailySessions)
    {
        var validDates = observations
            .Where(static value => value.IsComplete && value.IsValid)
            .Select(static value => value.ValueDate)
            .ToHashSet();
        var gracePermitted = validDates.Count >= minimumValidDailySessions;
        return expected
            .Where(date => !validDates.Contains(date)
                && !(gracePermitted && trailingGraceDates.Contains(date)))
            .ToArray();
    }

    static AcquisitionRange[] ContiguousRanges(
        IReadOnlyCollection<DateOnly> missing,
        IReadOnlyList<DateOnly> expected)
    {
        if (missing.Count == 0)
            return [];
        var expectedIndex = expected.Select((date, index) => (date, index))
            .ToDictionary(static value => value.date, static value => value.index);
        var ordered = missing.OrderBy(static value => value).ToArray();
        List<AcquisitionRange> ranges = [];
        var start = ordered[0];
        var end = start;
        for (var index = 1; index < ordered.Length; index++)
        {
            if (expectedIndex[ordered[index]] == expectedIndex[end] + 1)
            {
                end = ordered[index];
                continue;
            }
            ranges.Add(new(start, end));
            start = end = ordered[index];
        }
        ranges.Add(new(start, end));
        return [.. ranges];
    }

    static Guid AttemptId(
        MarketSeriesIdentity series,
        DateOnly startDate,
        DateOnly endDate,
        string normalizationVersion)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"62a4051d-7d1f-5b6d-9768-c2b076732dd0|{series.Format()}|{startDate:O}|{endDate:O}|{normalizationVersion}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    string ReplayKey(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<FuturesEodObservationReadModel> observations)
    {
        var content = new StringBuilder()
            .Append(startDate.ToString("O"))
            .Append('|').Append(endDate.ToString("O"))
            .Append('|').Append(options.CalculationConfigurationVersion);
        foreach (var value in observations)
            content.Append('|').Append(value.MarketSeriesIdentity.Format())
                .Append('|').Append(value.ValueDate.ToString("O"))
                .Append('|').Append(value.ContractId)
                .Append('|').Append(value.Close)
                .Append('|').Append(value.LastSourceSequence);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString())));
    }

    DateOnly LastCompletedTradingDate(DateOnly candidate)
    {
        var current = candidate;
        var now = timeProvider.GetUtcNow();
        while (!calendar.IsTradingDate(current) || calendar.GetSession(current).EndUtc > now)
            current = current.AddDays(-1);
        return current;
    }

    DateOnly[] TradingDates(DateOnly startDate, DateOnly endDate)
    {
        List<DateOnly> dates = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
            if (calendar.IsTradingDate(date)) dates.Add(date);
        return [.. dates];
    }

    async ValueTask<Dictionary<MarketSeriesIdentity, IReadOnlyList<FuturesEodObservationReadModel>>>
        ReadCoverageAsync(
            IReadOnlyCollection<MarketDataHistoricalSeriesRequest> series,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken)
    {
        var result = new Dictionary<MarketSeriesIdentity, IReadOnlyList<FuturesEodObservationReadModel>>();
        foreach (var item in series)
            result[item.SeriesIdentity] = await observationStore.GetRawEodRangeAsync(
                item.SeriesIdentity, startDate, endDate, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
