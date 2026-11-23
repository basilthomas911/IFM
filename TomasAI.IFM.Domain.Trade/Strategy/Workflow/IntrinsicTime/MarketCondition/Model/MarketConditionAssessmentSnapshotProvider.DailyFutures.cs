using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

public sealed partial class MarketConditionAssessmentSnapshotProvider
{
    public ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesAsync(
        MarketConditionAssessmentParameterSet p, DateTime at, CancellationToken token) => CaptureDailyFuturesCoreAsync(p, at, false, false, token);

    public ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesLegacyAsync(
        MarketConditionAssessmentParameterSet p, DateTime at, CancellationToken token) => CaptureDailyFuturesCoreAsync(p, at, true, false, token);

    public ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesMarketAsync(
        MarketConditionAssessmentParameterSet p, DateTime at, CancellationToken token) => CaptureDailyFuturesCoreAsync(p, at, false, true, token);

    async ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesCoreAsync(
        MarketConditionAssessmentParameterSet p, DateTime at, bool legacy, bool marketOnly, CancellationToken token)
    {
        if (preparedHistory is null) p.Validate();
        if (p.TargetHorizon != TimeFrameType.Daily) throw new ArgumentException("Daily futures requires Daily parameters.", nameof(p));
        for (var attempt = 0; attempt < (preparedHistory is null ? p.SnapshotCaptureAttempts : 1); attempt++)
        {
            token.ThrowIfCancellationRequested();
            var hasContract = marketData.TryGetOnTheRunFuturesContract(p.InstrumentRoot, out var contract);
            var before = marketData.GetFuturesMarketHealth(hasContract ? contract.ContractId : "");
            var observations = DailyFuturesGateCalculator.SourceIds.ToDictionary(id => id, id => Missing(id, at), StringComparer.Ordinal);
            AssessmentReferenceQuote? quote = null;
            if (hasContract && marketData.TryGetLastTickPrice(contract.ContractId, out var price))
            {
                if (price.Quote is { BidPrice: { } bid, AskPrice: { } ask } q)
                {
                    quote = new(bid, ask, q.BidSize, q.AskSize);
                    observations["ReferenceQuote"] = Observed("ReferenceQuote", q.EventTimestamp, q.ReceiveTimestamp, q.SourceSequence);
                }
                if (price.Trade is { } trade)
                    observations["LastTrade"] = Observed("LastTrade", trade.EventTimestamp, trade.ReceiveTimestamp, trade.SourceSequence)
                        with { Value = trade.LastPrice, Unit = "ES points" };
            }
            if (hasContract && atrCache is not null && atrCache.TryGetLatest(contract.ContractId, TimeFrameType.FiveMinutes, out var atr)
                && atr is { IsWarm: true, Metadata.IsValid: true, PeriodLength: 14, AtrValue: > 0 }
                && double.IsFinite(atr.AtrValue)
                && (!legacy || atr.AtrBaseline is > 0 && atr.AtrRatio is > 0
                    && double.IsFinite(atr.AtrBaseline.Value) && double.IsFinite(atr.AtrRatio.Value)))
            {
                var source = Observed("Atr14", atr.Metadata.MarketDataAsOfUtc, atr.Metadata.CalculatedAtUtc, atr.Metadata.SourceSequence);
                observations["Atr14"] = source with { Value = (decimal)atr.AtrValue, Unit = "ES points" };
                // Optional context only in v2; absent/invalid baseline data cannot veto ATR(14).
                if (atr.AtrBaseline is > 0 && double.IsFinite(atr.AtrBaseline.Value)
                    && (legacy || atr.AtrBaseline.Value <= (double)decimal.MaxValue))
                    observations["AtrBaseline"] = source with { SourceId = "AtrBaseline", Value = (decimal)atr.AtrBaseline.Value, Unit = "ES points" };
                if (atr.AtrRatio is > 0 && double.IsFinite(atr.AtrRatio.Value)
                    && (legacy || atr.AtrRatio.Value <= (double)decimal.MaxValue))
                    observations["AtrBaselineRatio"] = source with { SourceId = "AtrBaselineRatio", Value = (decimal)atr.AtrRatio.Value, Unit = "ratio" };
            }
            var sessionDate = calendar.GetValueDate(new DateTimeOffset(at));
            var session = calendar.GetSession(sessionDate);
            var open = marketOnly ? calendar.IsTradingDate(sessionDate) && at >= session.StartUtc.UtcDateTime && at < session.EndUtc.UtcDateTime
                : DailyFuturesGateCalculator.IsEntryWindow(calendar, at);
            observations["SessionCalendar"] = Checked("SessionCalendar", at);
            var risk = await events.ReadOnceAsync(new MarketConditionEventRiskConfiguration
            {
                HighImpactBeforeMinutes = p.HighImpactBeforeMinutes, HighImpactAfterMinutes = p.HighImpactAfterMinutes,
                RateDecisionBeforeMinutes = p.RateDecisionBeforeMinutes, RateDecisionAfterMinutes = p.RateDecisionAfterMinutes,
                RequiredEventCategories = ["HighImpact", "RateDecision"]
            }, at, token).ConfigureAwait(false);
            var eo = risk.Observation;
            observations["EventRiskCalendar"] = new() { SourceId = "EventRiskCalendar", ObservedAtUtc = eo.SourceTimestampUtc,
                ReceivedAtUtc = eo.ReceivedAtUtc, Sequence = eo.SequenceId, Availability = eo.Availability, Validity = eo.Validity,
                Reason = risk.DownloadEvidence?.Reason ?? "" };
            var after = marketData.GetFuturesMarketHealth(hasContract ? contract.ContractId : "");
            var stillHasContract = marketData.TryGetOnTheRunFuturesContract(p.InstrumentRoot, out var current);
            if (hasContract != stillHasContract || hasContract && current.ContractId != contract.ContractId
                || before.Generation != after.Generation || before.ValueDate != after.ValueDate || before.Running != after.Running) continue;
            // This is the ES contract's feed authority. A separate VX/options audit
            // cannot veto Daily futures; actor admission and execution controls remain upstream/downstream.
            var healthy = after.Running && after.Healthy;
            observations["FeedHealth"] = Observed("FeedHealth", after.ObservedAtUtc, after.ObservedAtUtc, after.Sequence)
                with { Availability = marketOnly || healthy ? MarketSourceAvailability.Available : MarketSourceAvailability.Unavailable,
                    Value = marketOnly ? healthy ? 1 : 0 : null, Reason = !marketOnly || healthy ? "" : "ES feed reports unhealthy; strategy eligibility must reject new exposure" };
            return new MarketConditionAssessmentSnapshot { SnapshotId = Guid.NewGuid(), MarketProfileId = p.MarketProfileId,
                InstrumentRoot = p.InstrumentRoot, TargetHorizon = p.TargetHorizon, ReferenceInstrumentId = hasContract ? contract.ContractId : "",
                EvaluatedAtUtc = at, Quote = quote, SessionState = open ? MarketSessionStatus.Open : MarketSessionStatus.Closed,
                EventContext = risk.Status switch { MarketEventRiskStatus.Clear => AssessmentEventContext.Clear,
                    MarketEventRiskStatus.Blocked => AssessmentEventContext.Elevated, _ => AssessmentEventContext.Unknown },
                Observations = observations.Values.ToArray(), CalendarEvidence = risk.DownloadEvidence }.Seal();
        }
        throw new InvalidOperationException("Reference contract or feed generation changed during Daily futures capture.");
    }
}

