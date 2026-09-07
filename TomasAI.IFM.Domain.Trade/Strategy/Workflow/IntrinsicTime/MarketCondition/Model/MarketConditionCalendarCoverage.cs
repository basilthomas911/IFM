using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Read-only, bounded calendar coverage policy. Never downloads or repairs data.</summary>
public static class MarketConditionCalendarCoverage
{
    public const int MaximumDownloadAgeSeconds = 86400;
    public const int MaximumCoveredDates = 3;

    public static async ValueTask<MarketConditionCalendarDownloadEvidence> CaptureAsync(
        IDownloadLogQueryApi queries, DateTime at, int beforeMinutes, int afterMinutes,
        CancellationToken cancellationToken)
    {
        if (at.Kind != DateTimeKind.Utc || beforeMinutes < 0 || afterMinutes < 0)
            throw Invalid("Calendar coverage requires a UTC timestamp and nonnegative windows.");
        var from = DateOnly.FromDateTime(at.AddMinutes(-afterMinutes));
        var to = DateOnly.FromDateTime(at.AddMinutes(beforeMinutes));
        if (to.DayNumber - from.DayNumber >= MaximumCoveredDates)
            throw Invalid("Calendar coverage exceeds its bounded three-date window.");

        var attempts = new List<MarketDataDownloadLogReadModel>();
        var selected = new List<MarketDataDownloadLogReadModel>();
        var reason = string.Empty;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var candidates = new List<MarketDataDownloadLogReadModel>();
            // ALL covers US; a filtered US download also covers it. No cross-country inference.
            foreach (var scope in new[] { "ALL", "US" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                var partition = new MarketDataDownloadPartition(MarketDataDownloadDataset.EconomicCalendar, "FMP", scope, date);
                var reply = await queries.GetHistoryAsync(partition, 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!reply.Success || reply.Value?.Attempts is null)
                    throw Invalid("Calendar DownloadLog query failed; coverage could not be established.");
                if (reply.Value.Attempts.Length > 1 ||
                    reply.Value.Attempts.Length == 0 && reply.Value.Continuation is not null)
                    throw Invalid("Calendar DownloadLog query returned an invalid page.");
                foreach (var row in reply.Value.Attempts)
                {
                    Validate(row, partition);
                    attempts.Add(row);
                    candidates.Add(row);
                }
            }

            // Do not hide a newer failed/partial refresh behind an older successful attempt.
            var latest = candidates.OrderByDescending(x => x.Outcome.RequestedAtUtc)
                .ThenBy(x => x.Outcome.Status == MarketDataDownloadStatus.Failed ? 0 : 1)
                .ThenBy(x => x.Outcome.FinishedAtUtc).ThenBy(x => x.Outcome.ImportCommandId).FirstOrDefault();
            var limitation = latest is null ? "CalendarDownloadNotConfirmed"
                : latest.Outcome.FinishedAtUtc > at || latest.ProjectedAtUtc > at ? "CalendarDownloadAfterCapture"
                : latest.Outcome.Status != MarketDataDownloadStatus.Completed ? "CalendarDownloadFailed"
                : (at - latest.Outcome.FinishedAtUtc).TotalSeconds >= MaximumDownloadAgeSeconds ? "CalendarDownloadStale"
                : string.Empty;
            if (reason.Length == 0) reason = limitation;
            if (latest is not null) selected.Add(latest);
        }

        return new()
        {
            CheckedAtUtc = at, FromDate = from, ToDate = to,
            MaximumDownloadAgeSeconds = MaximumDownloadAgeSeconds,
            CoverageConfirmed = reason.Length == 0,
            Reason = reason.Length == 0 ? "CalendarDownloadConfirmed" : reason,
            // A new date entering the event window requires a new coverage check.
            ValidUntilUtc = reason.Length != 0 ? null : new[]
            {
                selected.Min(x => x.Outcome.FinishedAtUtc.AddSeconds(MaximumDownloadAgeSeconds)),
                to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-beforeMinutes)
            }.Min(),
            Attempts = attempts.ToArray()
        };
    }

    static void Validate(MarketDataDownloadLogReadModel row, MarketDataDownloadPartition partition)
    {
        try
        {
            var outcome = row.Outcome;
            outcome.Validate();
            if (outcome.Dataset != partition.Dataset || outcome.Provider != partition.Provider ||
                outcome.Scope != partition.Scope || outcome.ValueDate != partition.ValueDate ||
                row.LogCommandId != MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId) ||
                row.PayloadSha256 != outcome.ComputeHash() || row.ProjectedAtUtc.Kind != DateTimeKind.Utc ||
                row.ProjectedAtUtc < outcome.FinishedAtUtc)
                throw new ArgumentException("DownloadLog provenance mismatch.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            throw Invalid("Calendar DownloadLog evidence is corrupt or belongs to another partition.");
        }
    }

    static InvalidOperationException Invalid(string message) => new(message);
}
