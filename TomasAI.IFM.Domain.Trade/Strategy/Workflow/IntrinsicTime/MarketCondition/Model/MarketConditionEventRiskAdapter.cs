using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

public interface IMarketConditionEventRiskAdapter
{
    ValueTask<MarketConditionEventRiskState> ReadOnceAsync(MarketConditionEventRiskConfiguration configuration,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken);
}

/// <summary>Captures US event-risk observations with authoritative calendar download evidence.</summary>
public sealed class MarketConditionEventRiskAdapter(IDbContextFactory storage,
    TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IDownloadLogQueryApi downloadLogs) : IMarketConditionEventRiskAdapter
{
    public async ValueTask<MarketConditionEventRiskState> ReadOnceAsync(
        MarketConditionEventRiskConfiguration configuration, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken)
    {
        if (configuration.RequiredEventCategories.Any(x => x is not ("HighImpact" or "RateDecision")))
            throw new InvalidOperationException("An unsupported required event-risk category was configured.");
        var before = Math.Max(configuration.HighImpactBeforeMinutes, configuration.RateDecisionBeforeMinutes);
        var after = Math.Max(configuration.HighImpactAfterMinutes, configuration.RateDecisionAfterMinutes);
        var coverage = await MarketConditionCalendarCoverage.CaptureAsync(downloadLogs,
            evaluationTimestampUtc, before, after, cancellationToken).ConfigureAwait(false);
        if (!coverage.CoverageConfirmed)
            return new MarketConditionEventRiskState
            {
                Status = MarketEventRiskStatus.Unknown,
                DownloadEvidence = coverage,
                Observation = CheckedNow("EventRiskCalendar", evaluationTimestampUtc)
                    with { Availability = MarketSourceAvailability.Unavailable }
            };
        var rows = await storage.MarketDataDb.GetEconomicCalendarsAsync(
            evaluationTimestampUtc.AddMinutes(-after), evaluationTimestampUtc.AddMinutes(before), "US",
            cancellationToken).ConfigureAwait(false);
        if (rows.Any(x => !x.IsValid || string.IsNullOrWhiteSpace(x.EventName)))
            throw new InvalidOperationException("Economic-calendar event identity is invalid.");

        var active = rows.Select(x => new
            {
                Value = x,
                Category = IsRateDecision(x.EventName) ? "RateDecision" :
                    string.Equals(x.Impact, "High", StringComparison.OrdinalIgnoreCase) ? "HighImpact" : string.Empty
            })
            .Where(x => configuration.RequiredEventCategories.Contains(x.Category, StringComparer.Ordinal))
            .Where(x => InWindow(x.Value.EventDate, x.Category, configuration, evaluationTimestampUtc))
            .OrderBy(x => Math.Abs((x.Value.EventDate - evaluationTimestampUtc).Ticks))
            .ThenBy(x => x.Value.EventDate).ThenBy(x => x.Value.EventName, StringComparer.Ordinal)
            .FirstOrDefault();
        return new MarketConditionEventRiskState
        {
            Status = active is null ? MarketEventRiskStatus.Clear : MarketEventRiskStatus.Blocked,
            EventId = active is null ? string.Empty : $"US|{active.Value.EventDate:O}|{active.Value.EventName}",
            Category = active?.Category ?? string.Empty,
            DownloadEvidence = coverage,
            Observation = CheckedNow("EventRiskCalendar", evaluationTimestampUtc)
        };
    }

    static MarketSourceObservation CheckedNow(string source, DateTime at) => new()
    {
        SourceId = source, SourceTimestampUtc = at, ReceivedAtUtc = at, SequenceId = at.Ticks,
        Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid
    };

    static bool IsRateDecision(string name) =>
        name.Contains("rate decision", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("FOMC", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Federal Reserve", StringComparison.OrdinalIgnoreCase);

    static bool InWindow(DateTime eventAt, string category, MarketConditionEventRiskConfiguration c, DateTime at)
    {
        var before = category == "RateDecision" ? c.RateDecisionBeforeMinutes : c.HighImpactBeforeMinutes;
        var after = category == "RateDecision" ? c.RateDecisionAfterMinutes : c.HighImpactAfterMinutes;
        return at >= eventAt.AddMinutes(-before) && at <= eventAt.AddMinutes(after);
    }
}
