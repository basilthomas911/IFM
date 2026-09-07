using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

public interface IMarketConditionAssessmentSnapshotProvider
{
    ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet parameters,
        DateTime evaluatedAtUtc, CancellationToken cancellationToken);
}

/// <summary>Captures market authorities only. Never starts feeds or queries options, fund mandates or brokers.</summary>
public sealed class MarketConditionAssessmentSnapshotProvider(IMarketDataApi marketData,
    IDbContextFactory storage, IMarketSessionCalendar calendar,
    IMarketConditionEventRiskAdapter events) : IMarketConditionAssessmentSnapshotProvider
{
    public async ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet p,
        DateTime at, CancellationToken cancellationToken)
    {
        p.Validate();
        for (var attempt = 0; attempt < p.SnapshotCaptureAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasContract = marketData.TryGetOnTheRunFuturesContract(p.InstrumentRoot, out var contract);
            var before = marketData.GetFuturesMarketHealth(hasContract?contract.ContractId:string.Empty);
            var observations = p.Sources.ToDictionary(x => x.SourceId, x => Missing(x.SourceId, at), StringComparer.Ordinal);
            AssessmentReferenceQuote? quote = null;
            if (hasContract && marketData.TryGetLastTickPrice(contract.ContractId, out var price))
            {
                if (price.Quote is { BidPrice: { } bid, AskPrice: { } ask } q)
                {
                    quote = new(bid, ask, q.BidSize, q.AskSize);
                    observations["ReferenceQuote"] = Observed("ReferenceQuote", q.EventTimestamp, q.ReceiveTimestamp, q.SourceSequence);
                }
                if (price.Trade is { } t) observations["LastTrade"] = Observed("LastTrade", t.EventTimestamp, t.ReceiveTimestamp, t.SourceSequence) with { Value = t.LastPrice, Unit = "price" };
                // Consume an existing measured signal; do not rebuild ATR or a volatility regime.
                var atr = await storage.MarketDataDb.GetLastFuturesAtrSignalAsync(contract.ContractId, price.ValueDate,
                    TimeFrameType.OneMinute, 14, cancellationToken).ConfigureAwait(false);
                if (atr?.Metadata is { IsValid: true } metadata)
                {
                    if (!double.IsFinite(atr.AtrValue) || !double.IsFinite(atr.TrueRange) || atr.AtrValue <= 0)
                        throw new InvalidOperationException("Authoritative normalized movement contains invalid numeric values.");
                    observations["NormalizedMovement"] = new()
                    {
                        SourceId = "NormalizedMovement", ObservedAtUtc = metadata.MarketDataAsOfUtc.UtcDateTime,
                        ReceivedAtUtc = metadata.CalculatedAtUtc.UtcDateTime, Sequence = metadata.SourceSequence,
                        Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid,
                        Value = (decimal)Math.Abs(atr.TrueRange / atr.AtrValue), Unit = "ATR ratio"
                    };
                }
            }
            if (marketData.TryGetOnTheRunFuturesContract("VX", out var vx) && marketData.TryGetLastTickPrice(vx.ContractId, out var vxPrice) && vxPrice.Trade is { } vxTrade)
            {
                var target = at.AddMinutes(-5);
                var bars = await storage.MarketDataDb.GetFuturesBarDataAsync(vx.ContractId, "VX", vxPrice.ValueDate, target.AddMinutes(-5), target).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var baseline = bars.Where(x => x.BarDate <= target && x.BarValue > 0m).OrderByDescending(x => x.BarDate).FirstOrDefault();
                if (baseline is not null)
                    observations["VolatilityChange"] = Observed("VolatilityChange", vxTrade.EventTimestamp, vxTrade.ReceiveTimestamp, vxTrade.SourceSequence)
                        with { Value = (vxTrade.LastPrice - baseline.BarValue) / baseline.BarValue, Unit = "5 minute relative change", Reason = $"Baseline {baseline.BarDate:O}" };
            }
            var valueDate = calendar.GetValueDate(new DateTimeOffset(at));
            var open = false;
            if (calendar.IsTradingDate(valueDate))
            {
                var session = calendar.GetSession(valueDate);
                open = at >= session.StartUtc.UtcDateTime && at < session.EndUtc.UtcDateTime;
            }
            observations["SessionCalendar"] = Checked("SessionCalendar", at);
            var eventState = await events.ReadOnceAsync(new MarketConditionEventRiskConfiguration
            {
                HighImpactBeforeMinutes = p.HighImpactBeforeMinutes, HighImpactAfterMinutes = p.HighImpactAfterMinutes,
                RateDecisionBeforeMinutes = p.RateDecisionBeforeMinutes, RateDecisionAfterMinutes = p.RateDecisionAfterMinutes,
                RequiredEventCategories = ["HighImpact", "RateDecision"]
            }, at, cancellationToken).ConfigureAwait(false);
            var eo = eventState.Observation;
            observations["EventRiskCalendar"] = new()
            {
                SourceId = "EventRiskCalendar", ObservedAtUtc = eo.SourceTimestampUtc, ReceivedAtUtc = eo.ReceivedAtUtc,
                Sequence = eo.SequenceId, Availability = eo.Availability, Validity = eo.Validity,
                Reason = eventState.DownloadEvidence?.Reason ?? ""
            };
            var after = marketData.GetFuturesMarketHealth(hasContract?contract.ContractId:string.Empty);
            var stillHasContract = marketData.TryGetOnTheRunFuturesContract(p.InstrumentRoot, out var latestContract);
            if (hasContract != stillHasContract || hasContract && latestContract.ContractId != contract.ContractId || before.ValueDate != after.ValueDate || before.Running != after.Running || before.Generation != after.Generation)
                continue;
            var healthy = after.Running && after.Healthy;
            observations["FeedHealth"] = Observed("FeedHealth",after.ObservedAtUtc,after.ObservedAtUtc,after.Sequence) with
            {
                Availability = healthy ? MarketSourceAvailability.Available : MarketSourceAvailability.Unavailable,
                Reason = healthy ? "Feed and latest-value cache operational: "+after.Generation : "MC.ASSESSMENT.FEED.UNAVAILABLE"
            };
            cancellationToken.ThrowIfCancellationRequested();
            return new MarketConditionAssessmentSnapshot
            {
                SnapshotId = Guid.NewGuid(), MarketProfileId = p.MarketProfileId, InstrumentRoot = p.InstrumentRoot, TargetHorizon = p.TargetHorizon,
                ReferenceInstrumentId = hasContract ? contract.ContractId : "", EvaluatedAtUtc = at, Quote = quote,
                SessionState = open ? MarketSessionStatus.Open : MarketSessionStatus.Closed,
                EventContext = eventState.Status switch { MarketEventRiskStatus.Clear => AssessmentEventContext.Clear, MarketEventRiskStatus.Blocked => AssessmentEventContext.Elevated, _ => AssessmentEventContext.Unknown },
                Observations = observations.Values.ToArray(), CalendarEvidence = eventState.DownloadEvidence
            }.Seal();
        }
        throw new InvalidOperationException("Market reference or feed epoch changed during every bounded snapshot capture attempt.");
    }

    static AssessmentObservation Observed(string id, DateTimeOffset at, DateTimeOffset received, long sequence) => new()
    { SourceId = id, ObservedAtUtc = at.UtcDateTime, ReceivedAtUtc = received.UtcDateTime, Sequence = sequence, Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid };
    static AssessmentObservation Checked(string id, DateTime at) => Observed(id, new(at), new(at), at.Ticks);
    static AssessmentObservation Missing(string id, DateTime at) => Checked(id, at) with
    { Availability = MarketSourceAvailability.Unavailable, Reason = $"MC.ASSESSMENT.{id.ToUpperInvariant()}.MISSING" };
}
