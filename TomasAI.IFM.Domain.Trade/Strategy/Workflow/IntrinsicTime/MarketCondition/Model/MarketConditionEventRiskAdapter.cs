using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Application.Storage;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
/// <summary>Captures US event-risk observations with authoritative calendar download evidence.</summary>
public sealed class MarketConditionEventRiskAdapter(IDbContextFactory storage, TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IDownloadLogQueryApi downloadLogs,
    bool preparedOnly = false) : IMarketConditionEventRiskAdapter
{
    sealed record CalendarDay(long Generation, DateTime CapturedAtUtc, MarketConditionCalendarDownloadEvidence Coverage, EconomicCalendarReadModel[] Rows);
    readonly ConcurrentDictionary<(DateOnly From, DateOnly To, int Before, int After), Lazy<Task<CalendarDay>>> days = new();

    public ValueTask<MarketConditionEventRiskState> ReadOnceAsync(MarketConditionEventRiskConfiguration configuration, DateTime evaluationTimestampUtc, CancellationToken cancellationToken) =>
        ReadCoreAsync(configuration, evaluationTimestampUtc, cancellationToken, preparedOnly);

    /// <summary>Background-only loading and validation. Live reads never call this operation.</summary>
    public ValueTask<MarketConditionEventRiskState> WarmAsync(MarketConditionEventRiskConfiguration configuration, DateTime at, CancellationToken token) =>
        ReadCoreAsync(configuration, at, token, false);

    async ValueTask<MarketConditionEventRiskState> ReadCoreAsync(MarketConditionEventRiskConfiguration configuration,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken, bool readyOnly)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        if (!readyOnly && configuration.RequiredEventCategories.Any(x => x is not ("HighImpact" or "RateDecision")))
            throw new InvalidOperationException("An unsupported required event-risk category was configured.");
        var before = Math.Max(configuration.HighImpactBeforeMinutes, configuration.RateDecisionBeforeMinutes);
        var after = Math.Max(configuration.HighImpactAfterMinutes, configuration.RateDecisionAfterMinutes);
        var from = DateOnly.FromDateTime(evaluationTimestampUtc.AddMinutes(-after));
        var to = DateOnly.FromDateTime(evaluationTimestampUtc.AddMinutes(before));
        var key = (from, to, before, after);
        CalendarDay day = null!;
        if (readyOnly)
        {
            if (!days.TryGetValue(key, out var ready) || !ready.IsValueCreated || !ready.Value.IsCompletedSuccessfully)
                return Unavailable("CalendarCacheNotReady");
            day = ready.Value.Result;
            if (day.Generation != ReadCacheInvalidation.Calendar || day.CapturedAtUtc > evaluationTimestampUtc
                || day.Coverage.ValidUntilUtc <= evaluationTimestampUtc)
                return Unavailable("CalendarCacheInvalidated");
            WorkflowReadCacheDiagnostics.Log.CalendarReuse();
        }
        for (var attempt = 0; !readyOnly && attempt < 3; attempt++)
        {
            if (days.Count >= 4 && !days.ContainsKey(key)) days.Clear();
            var generation = ReadCacheInvalidation.Calendar;
            var pending = days.GetOrAdd(key, _ => new(() => LoadAsync(generation), LazyThreadSafetyMode.ExecutionAndPublication));
            var reused = pending.IsValueCreated && pending.Value.IsCompletedSuccessfully;
            try { day = await pending.Value.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch { if (pending.IsValueCreated && pending.Value.IsCompleted) days.TryRemove(new(key,pending)); throw; }
            if (day.Generation != ReadCacheInvalidation.Calendar || day.CapturedAtUtc > evaluationTimestampUtc
                || day.Coverage.CoverageConfirmed && day.Coverage.ValidUntilUtc <= evaluationTimestampUtc)
            { days.TryRemove(new(key,pending)); day = null!; continue; }
            if (!day.Coverage.CoverageConfirmed) days.TryRemove(new(key,pending));
            else if (reused) WorkflowReadCacheDiagnostics.Log.CalendarReuse();
            break;
        }
        if (day is null) throw new InvalidOperationException("Calendar changed during cache capture.");
        var coverage = day.Coverage with { Attempts = [..day.Coverage.Attempts] };
        if (!coverage.CoverageConfirmed)
            return new MarketConditionEventRiskState
            {
                Status = MarketEventRiskStatus.Unknown,
                DownloadEvidence = coverage,
                Observation = CheckedNow("EventRiskCalendar", evaluationTimestampUtc)with
                {
                    Availability = MarketSourceAvailability.Unavailable
                }
            };
        var rows = day.Rows;
        var active = rows.Select(x => new { Value = x, Category = ((IsRateDecision(x.EventName)) switch
        {
            true => "RateDecision",
            _ => string.Equals(x.Impact, "High", StringComparison.OrdinalIgnoreCase) ? "HighImpact" : string.Empty
        }) }).Where(x => configuration.RequiredEventCategories.Contains(x.Category, StringComparer.Ordinal)).Where(x => InWindow(x.Value.EventDate, x.Category, configuration, evaluationTimestampUtc)).OrderBy(x => Math.Abs((x.Value.EventDate - evaluationTimestampUtc).Ticks)).ThenBy(x => x.Value.EventDate).ThenBy(x => x.Value.EventName, StringComparer.Ordinal).FirstOrDefault();
        return new MarketConditionEventRiskState
        {
            Status = active is null ? MarketEventRiskStatus.Clear : MarketEventRiskStatus.Blocked,
            EventId = active is null ? string.Empty : $"US|{active.Value.EventDate:O}|{active.Value.EventName}",
            Category = active?.Category ?? string.Empty,
            DownloadEvidence = coverage,
            Observation = CheckedNow("EventRiskCalendar", evaluationTimestampUtc)
        };

        async Task<CalendarDay> LoadAsync(long generation)
        {
            var evidence = await MarketConditionCalendarCoverage.CaptureAsync(downloadLogs, evaluationTimestampUtc, before, after, cancellationToken).ConfigureAwait(false);
            if (!evidence.CoverageConfirmed) return new(generation, evaluationTimestampUtc, evidence, []);
            var events = await storage.MarketDataDb.GetEconomicCalendarsAsync(new EconomicCalendarDateRange(
                from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), "US"), cancellationToken).ConfigureAwait(false);
            if (events.Any(x => !x.IsValid || string.IsNullOrWhiteSpace(x.EventName))) throw new InvalidOperationException("Economic-calendar event identity is invalid.");
            WorkflowReadCacheDiagnostics.Log.CalendarLoad();
            return new(generation, evaluationTimestampUtc, evidence, events.ToArray());
        }

        MarketConditionEventRiskState Unavailable(string reason) => new()
        {
            Status = MarketEventRiskStatus.Unknown,
            DownloadEvidence = new() { CheckedAtUtc = evaluationTimestampUtc, FromDate = from, ToDate = to,
                CoverageConfirmed = false, Reason = reason, MaximumDownloadAgeSeconds = MarketConditionCalendarCoverage.MaximumDownloadAgeSeconds },
            Observation = CheckedNow("EventRiskCalendar", evaluationTimestampUtc) with { Availability = MarketSourceAvailability.Unavailable }
        };
    }

    static MarketSourceObservation CheckedNow(string source, DateTime at) => new()
    {
        SourceId = source,
        SourceTimestampUtc = at,
        ReceivedAtUtc = at,
        SequenceId = at.Ticks,
        Availability = MarketSourceAvailability.Available,
        Validity = MarketSourceValidity.Valid
    };
    static bool IsRateDecision(string name) => name.Contains("rate decision", StringComparison.OrdinalIgnoreCase) || name.Contains("FOMC", StringComparison.OrdinalIgnoreCase) || name.Contains("Federal Reserve", StringComparison.OrdinalIgnoreCase);
    static bool InWindow(DateTime eventAt, string category, MarketConditionEventRiskConfiguration c, DateTime at)
    {
        var before = string.Equals(category, "RateDecision", StringComparison.Ordinal) ? c.RateDecisionBeforeMinutes : c.HighImpactBeforeMinutes;
        var after = string.Equals(category, "RateDecision", StringComparison.Ordinal) ? c.RateDecisionAfterMinutes : c.HighImpactAfterMinutes;
        return at >= eventAt.AddMinutes(-before) && at <= eventAt.AddMinutes(after);
    }
}
