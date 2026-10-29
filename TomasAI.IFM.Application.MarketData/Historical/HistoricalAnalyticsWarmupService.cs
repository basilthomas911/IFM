using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Historical;
/// <summary>
/// Ensures sufficient historical coverage and replays stored Daily observations in order.
/// </summary>
public sealed class HistoricalAnalyticsWarmupService : IDisposable
{
    readonly ILogger logger;
    readonly HistoricalAnalyticsWarmupOptions options;
    readonly HistoricalDataLoader loader;
    readonly IHistoricalObservationStore observationStore;
    readonly IHistoricalDailyReplayPublisher dailyReplayPublisher;
    readonly IMarketSessionCalendar calendar;
    readonly TimeProvider timeProvider;
    readonly SemaphoreSlim gate = new(1, 1);
    string lastCompletedReplayKey = string.Empty;
    /// <summary>The last successfully published baseline; retained when acquisition fails.</summary>
    public DateOnly? BaselineValueDate { get; private set; }
    DateOnly? expectedBaselineDate;
    /// <summary>True when more than one completed trading session is missing, matching the acquisition policy.</summary>
    public bool BaselineIsStale => BaselineValueDate is { } baseline && expectedBaselineDate is { } end
        && baseline < end && TradingDates(baseline.AddDays(1), end).Length > 1;
    public HistoricalAnalyticsWarmupService(HistoricalAnalyticsWarmupOptions options, HistoricalDataLoader loader, IHistoricalObservationStore observationStore, IHistoricalDailyReplayPublisher dailyReplayPublisher, IMarketSessionCalendar calendar, TimeProvider timeProvider, ILogger<HistoricalAnalyticsWarmupService>? logger = null)
    {
        this.logger = logger ?? NullLogger<HistoricalAnalyticsWarmupService>.Instance;
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        this.observationStore = observationStore ?? throw new ArgumentNullException(nameof(observationStore));
        this.dailyReplayPublisher = dailyReplayPublisher ?? throw new ArgumentNullException(nameof(dailyReplayPublisher));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<HistoricalAnalyticsWarmupResult> EnsureAsync(MarketDataHistoricalRequest template, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var result = await EnsureCoreAsync(template, cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
            {
                var seriesCount = template.Series.Length;
                var elapsedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                RemainingApplicationDiagnosticLog.Write1(logger, null, nameof(HistoricalAnalyticsWarmupService), nameof(EnsureAsync), template.DataLoadAttemptId, template.StartDate, template.EndDate, seriesCount, result.Outcome, elapsedMilliseconds);
            }
            return result;
        }
        catch (Exception exception)
        {
            ApplicationDiagnosticLog.Write1(logger, exception, nameof(HistoricalAnalyticsWarmupService), nameof(EnsureAsync), template.DataLoadAttemptId, template.StartDate, template.EndDate);
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Keep this bounded explicit loop to avoid additional iterator allocations and preserve early-exit or sequential asynchronous processing in the market-data path.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    async ValueTask<HistoricalAnalyticsWarmupResult> EnsureCoreAsync(MarketDataHistoricalRequest template, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!options.Enabled)
            return new(HistoricalAnalyticsWarmupOutcome.Disabled, default, default, 0, 0, null);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var endDate = LastCompletedTradingDate(template.EndDate);
            expectedBaselineDate = endDate;
            if (BaselineValueDate > endDate) BaselineValueDate = null;
            logger.LogInformation("Daily analytics warmup effective range: TargetValueDate={ValueDate}; LastCompletedDate={EndDate}; AttemptId={AttemptId}",
                template.EndDate, endDate, template.DataLoadAttemptId);
            foreach (var series in template.Series)
            {
                try
                {
                    var restored = await dailyReplayPublisher.RestoreAsync(series.SeriesIdentity, template.EndDate,
                        template.AnalyticsTargetContractId, endDate, cancellationToken).ConfigureAwait(false);
                    if (restored.HasValue) BaselineValueDate = restored;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    logger.LogWarning(error, "Daily checkpoint restoration failed; trying stored observations. Series={Series}; AttemptId={AttemptId}",
                        series.SeriesIdentity, template.DataLoadAttemptId);
                }
            }
            var startDate = endDate.AddDays(-(options.LookbackCalendarDays - 1));
            var expected = TradingDates(startDate, endDate);
            var coverage = await ReadCoverageAsync(template.Series, startDate, endDate, cancellationToken).ConfigureAwait(false);
            var insufficientSeries = template.Series.Select(series => new { Series = series, ValidSessionCount = ValidSessionCount(coverage[series.SeriesIdentity]) }).Where(value => value.ValidSessionCount < options.MinimumValidDailySessions || HasStaleTail(coverage[value.Series.SeriesIdentity], expected)).ToArray();
            HistoricalDataLoaderState? loaded = null;
            // Make existing durable history usable before any provider request can fail or wait.
            var replayedBeforeAcquisition = coverage.All(pair => ValidSessionCount(pair.Value) >= options.MinimumValidDailySessions)
                && await ReplayCoverageAsync(coverage, template, startDate, endDate, cancellationToken).ConfigureAwait(false);
            if (insufficientSeries.Length > 0 || template.Series.Any(series =>
                expected.Any(date => date > LastValidDate(coverage[series.SeriesIdentity]))))
            {
                // Read storage first. Provider availability is needed only when
                // acquisition is required, and never advances the API ValueDate.
                var availableEnd = await loader.GetAvailableEndAsync(template with { StartDate = startDate, EndDate = endDate }, cancellationToken).ConfigureAwait(false);
                if (availableEnd is { } publishedEnd)
                {
                    var requestedEnd = endDate;
                    while (!calendar.IsTradingDate(endDate) || calendar.GetSession(endDate).EndUtc > publishedEnd)
                    {
                        if (endDate <= DateOnly.MinValue.AddDays(1))
                            throw new InvalidDataException("No completed trading session exists within provider availability.");
                        endDate = endDate.AddDays(-1);
                    }
                    expectedBaselineDate = endDate;
                    startDate = endDate.AddDays(-(options.LookbackCalendarDays - 1));
                    expected = TradingDates(startDate, endDate);
                    coverage = await ReadCoverageAsync(template.Series, startDate, endDate, cancellationToken).ConfigureAwait(false);
                    // Availability is known, so acquire even one missing published
                    // session. Never ask for a session the provider cannot supply.
                    insufficientSeries = template.Series.Select(series => new { Series = series, ValidSessionCount = ValidSessionCount(coverage[series.SeriesIdentity]) })
                        .Where(value => value.ValidSessionCount < options.MinimumValidDailySessions
                            || expected.Any(date => date > LastValidDate(coverage[value.Series.SeriesIdentity]))).ToArray();
                    logger.LogInformation("Daily analytics provider boundary: RequestedCompletedDate={RequestedDate}; PublishedEndUtc={PublishedEndUtc}; AcquisitionEndDate={EndDate}",
                        requestedEnd, publishedEnd, endDate);
                }
                foreach (var insufficient in insufficientSeries)
                {
                    var acquisitionStart = insufficient.ValidSessionCount < options.MinimumValidDailySessions ? startDate
                        : expected.First(date => date > LastValidDate(coverage[insufficient.Series.SeriesIdentity]));
                    loaded = await loader.ExecuteAsync(template with { DataLoadAttemptId = AttemptId(insufficient.Series.SeriesIdentity, acquisitionStart, endDate, options.NormalizationVersion), Series = [insufficient.Series], StartDate = acquisitionStart, EndDate = endDate, MaximumCostUsd = options.MaximumCostUsd, MaximumBytes = options.MaximumBytes, NormalizationVersion = options.NormalizationVersion }, cancellationToken).ConfigureAwait(false);
                }

                coverage = await ReadCoverageAsync(template.Series, startDate, endDate, cancellationToken).ConfigureAwait(false);
            }

            var remainingInsufficient = template.Series.Select(series => new { series.SeriesIdentity, ValidSessionCount = ValidSessionCount(coverage[series.SeriesIdentity]) }).Where(value => value.ValidSessionCount < options.MinimumValidDailySessions).ToArray();
            if (remainingInsufficient.Length > 0)
                throw new InvalidDataException("Historical coverage remains insufficient after the one-year acquisition: " + string.Join(", ", remainingInsufficient.Select(value => $"{value.SeriesIdentity.Format()} has {value.ValidSessionCount}/{options.MinimumValidDailySessions} valid Daily sessions")) + ".");
            var remainingMissing = template.Series.Sum(series => expected.Count(date => !coverage[series.SeriesIdentity].Any(value => value.ValueDate == date && value.IsComplete && value.IsValid)));
            var esCount = coverage.Where(static pair => pair.Key.FuturesSeriesId is { } series && series.RootSymbol.Equals("ES", StringComparison.OrdinalIgnoreCase)).SelectMany(static pair => pair.Value).Where(static value => value.IsComplete && value.IsValid).Select(static value => value.ValueDate).Distinct().Count();
            if (esCount < options.MinimumValidDailySessions)
                throw new InvalidDataException($"ES historical coverage has {esCount} valid Daily sessions; {options.MinimumValidDailySessions} are required.");
            var replayed = await ReplayCoverageAsync(coverage, template, startDate, endDate, cancellationToken).ConfigureAwait(false);
            replayed |= replayedBeforeAcquisition;

            return new((insufficientSeries.Length > 0) switch
            {
                true => HistoricalAnalyticsWarmupOutcome.AcquiredAndReplayed,
                false => (replayed) switch
                {
                    true => HistoricalAnalyticsWarmupOutcome.ReplayedFromStorage,
                    false => HistoricalAnalyticsWarmupOutcome.AlreadyCurrent
                }}, startDate, endDate, esCount, remainingMissing, loaded);
        }
        finally
        {
            gate.Release();
        }
    }

    async ValueTask<bool> ReplayCoverageAsync(
        Dictionary<MarketSeriesIdentity, IReadOnlyList<FuturesEodObservationReadModel>> coverage,
        MarketDataHistoricalRequest template, DateOnly startDate, DateOnly endDate, CancellationToken token)
    {
        var ordered = coverage.OrderBy(static pair => pair.Key.Format(), StringComparer.Ordinal)
            .SelectMany(static pair => pair.Value.Where(static value => value.IsComplete && value.IsValid)
                .GroupBy(static value => value.ValueDate)
                .Select(static group => group.OrderByDescending(value => value.LastMarketEventUtc).First())
                .OrderBy(static value => value.ValueDate)).ToArray();
        var replayKey = ReplayKey(startDate, endDate, ordered) + "|" + template.EndDate + "|" + template.AnalyticsTargetContractId;
        if (string.Equals(lastCompletedReplayKey, replayKey, StringComparison.Ordinal)) return false;
        var latestEsDate = ordered.Where(static value => value.MarketSeriesIdentity.FuturesSeriesId is { RootSymbol: "ES" })
            .Select(static value => (DateOnly?)value.ValueDate).Max();
        if (BaselineValueDate.HasValue && latestEsDate <= BaselineValueDate) return false;
        await dailyReplayPublisher.PublishAsync(ordered, template.EndDate, template.AnalyticsTargetContractId, token).ConfigureAwait(false);
        lastCompletedReplayKey = replayKey;
        BaselineValueDate = latestEsDate;
        return true;
    }

    // Permit one not-yet-published completed session, but never treat weeks-old coverage as current.
    static DateOnly LastValidDate(IReadOnlyList<FuturesEodObservationReadModel> observations) =>
        observations.Where(value => value.IsComplete && value.IsValid).Select(value => value.ValueDate).DefaultIfEmpty().Max();
    static bool HasStaleTail(IReadOnlyList<FuturesEodObservationReadModel> observations, DateOnly[] expected) =>
        expected.Count(date => date > LastValidDate(observations)) > 1;

    static int ValidSessionCount(IReadOnlyList<FuturesEodObservationReadModel> observations) => observations.Where(static value => value.IsComplete && value.IsValid).Select(static value => value.ValueDate).Distinct().Count();
    static Guid AttemptId(MarketSeriesIdentity series, DateOnly startDate, DateOnly endDate, string normalizationVersion)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"62a4051d-7d1f-5b6d-9768-c2b076732dd0|{series.Format()}|{startDate:O}|{endDate:O}|{normalizationVersion}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    string ReplayKey(DateOnly startDate, DateOnly endDate, IReadOnlyList<FuturesEodObservationReadModel> observations)
    {
        var content = new StringBuilder().Append(startDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(endDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(options.CalculationConfigurationVersion);
        foreach (var value in observations)
            content.Append('|').Append(value.MarketSeriesIdentity.Format()).Append('|').Append(value.ValueDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.ContractId).Append('|').Append(value.Close.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.LastSourceSequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
            if (calendar.IsTradingDate(date))
                dates.Add(date);
        return[..dates];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Keep this bounded explicit loop to avoid additional iterator allocations and preserve early-exit or sequential asynchronous processing in the market-data path.")]
    async ValueTask<Dictionary<MarketSeriesIdentity, IReadOnlyList<FuturesEodObservationReadModel>>> ReadCoverageAsync(IReadOnlyCollection<MarketDataHistoricalSeriesRequest> series, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var result = new Dictionary<MarketSeriesIdentity, IReadOnlyList<FuturesEodObservationReadModel>>();
        foreach (var item in series)
            result[item.SeriesIdentity] = await observationStore.GetRawEodRangeAsync(item.SeriesIdentity, startDate, endDate, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private int disposedGateOwner;
    /// <summary>Releases the owned synchronization gate after the host has stopped its operations.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposedGateOwner, 1) != 0)
            return;
        gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
