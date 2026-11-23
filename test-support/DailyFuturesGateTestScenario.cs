using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Tests.Support;

internal static class DailyFuturesGateTestScenario
{
    public static IEnumerable<object[]> Cases() => new (string, string?)[] {
        ("long",null),("short",null),("atr-below-nine",null),("open",null),("shallow",null),("zero-depth",null),("max-spread",null),
        ("no-baseline",null),("invalid-baseline",null),("stale-baseline",null),
        ("range",null),("opposed",null),("not-dc",null),
        ("premarket",null),("cutoff",null),("stale-signal","SIGNAL_STALE"),
        ("cold-atr","ATR_NOT_READY"),("stale-atr","ATR_STALE"),("stale-quote","QUOTE_UNAVAILABLE"),
        ("crossed","QUOTE_INVALID"),("wide",null),
        ("feed","FEED_UNAVAILABLE"),("calendar","CALENDAR_UNAVAILABLE"),("blackout",null),
        ("closed",null),("feed-unhealthy",null),("inherited",null),("missing-session","SESSION_UNAVAILABLE"),
        ("wrong-contract","SIGNAL_IDENTITY_MISMATCH") }.Select(x => new object[] { x.Item1, x.Item2! });

    public static (ExecuteMarketConditionAssessmentCommand Command, MarketConditionAssessmentSnapshot Snapshot) Create(string scenario = "long", DateTime? timestamp = null, string contractId = "ES20261218")
    {
        var at = timestamp ?? new DateTime(2026,11,19,15,0,0,DateTimeKind.Utc);
        if (scenario == "open") at = new(2026,11,19,14,30,0,DateTimeKind.Utc);
        if (scenario == "premarket") at = new(2026,11,19,14,29,59,DateTimeKind.Utc);
        if (scenario == "cutoff") at = new(2026,11,19,20,55,0,DateTimeKind.Utc);
        var c = AssessmentFixture.Command(TimeFrameType.Daily, at, contractId);
        var entity = c.WorkflowEntityId with { Assignment = new(1,1,"Daily futures","Future","ES",TimeFrameType.Daily,default,1,at.AddDays(-1),null) };
        var t = c.TriggerEvent with { CreatedOn = at.AddSeconds(scenario == "stale-signal" ? -31 : -1),
            FuturesItiSignal = c.TriggerEvent.FuturesItiSignal! with { IntrinsicTime = at.AddSeconds(scenario == "stale-signal" ? -31 : -1),
                IntrinsicTimeTrend = scenario == "short" ? IntrinsicTimeTrendType.DownTrend : IntrinsicTimeTrendType.UpTrend,
                IntrinsicTimeMode = scenario == "not-dc" ? IntrinsicTimeModeType.TrendExtremeChanged : IntrinsicTimeModeType.TrendDirectionChanged } };
        var bar = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes,1,new DateTimeOffset(at).AddSeconds(-30),new CmeFuturesMarketSessionCalendar())[0];
        var source = bar.EndUtc.UtcDateTime.AddSeconds(-1);
        var direction = scenario == "range" ? RegimeDirection.Neutral : scenario is "short" or "opposed" ? RegimeDirection.Down : RegimeDirection.Up;
        var regime = c.RegimeResultEnvelope.ReadRegimeResult() with { EntityId = entity, MarketDataAsOfUtc = source,
            Reasons = [new() { Code = EmaStackRegimePolicy.ReasonCode }],
            Decision = c.RegimeResultEnvelope.ReadRegimeResult().Decision with { Direction = direction, TrendPhase = TrendRegimePhase.Unknown,
                Restrictions = scenario == "inherited" ? [RegimeRestriction.NoNewTrade] : [],
                VolatilityChange = VolatilityRegimeChange.Unknown, StructureClassification = direction == RegimeDirection.Neutral ? MarketStructureClassification.Ranging : MarketStructureClassification.Trending } };
        var envelope = StrategyStageResultEnvelope.CreateRegime(regime);
        var view = c.WorkflowView with { EntityId = entity, TriggerEvent = t, RegimeDiscovery = c.WorkflowView.RegimeDiscovery with { Result = envelope } };
        var id = new MarketConditionAssessmentExecutionId(entity,c.WorkflowId);
        c = c with { EntityId = id, WorkflowView = view, TriggerEvent = t, CalculationPolicy = DailyFuturesGatePolicy.Version,
            RegimeResultEnvelope = envelope, RegimePayloadSha256 = envelope.PayloadSha256,
            Subject = new(c.Subject.ActorType, ExecuteMarketConditionAssessmentCommand.Actor, ExecuteMarketConditionAssessmentCommand.Verb,id.Format()) };
        var observations = new[] { "ReferenceQuote","LastTrade","FeedHealth","SessionCalendar","EventRiskCalendar","Atr14","AtrBaseline","AtrBaselineRatio" }
            .Select(name => new AssessmentObservation { SourceId = name, ObservedAtUtc = name.StartsWith("Atr",StringComparison.Ordinal) ? source : at,
                ReceivedAtUtc = at, Sequence = 1, Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid,
                Value = name == "LastTrade" ? 5000 : name == "AtrBaselineRatio" ? 1 : name.StartsWith("Atr",StringComparison.Ordinal) ? 5 : null,
                Unit = name == "AtrBaselineRatio" ? "ratio" : name.StartsWith("Atr",StringComparison.Ordinal) ? "ES points" : "" }).ToArray();
        if (scenario is "cold-atr" or "stale-atr") observations = observations.Select(o => o.SourceId.StartsWith("Atr",StringComparison.Ordinal)
            ? o with { Availability = scenario == "cold-atr" ? MarketSourceAvailability.Unavailable : MarketSourceAvailability.Available,
                ObservedAtUtc = scenario == "stale-atr" ? source.AddMinutes(-5) : source } : o).ToArray();
        if (scenario is "no-baseline" or "invalid-baseline" or "stale-baseline")
            observations = observations.Select(o => o.SourceId is "AtrBaseline" or "AtrBaselineRatio"
                ? o with { Availability = scenario == "no-baseline" ? MarketSourceAvailability.Unavailable : o.Availability,
                    Value = scenario == "no-baseline" ? null : scenario == "invalid-baseline" ? -1 : o.Value,
                    ObservedAtUtc = scenario == "stale-baseline" ? source.AddDays(-1) : o.ObservedAtUtc, Sequence = 99 } : o).ToArray();
        if (scenario == "stale-quote") observations = observations.Select(o => o.SourceId == "ReferenceQuote" ? o with { ObservedAtUtc = at.AddSeconds(-3) } : o).ToArray();
        if (scenario == "missing-session") observations = observations.Select(o => o.SourceId == "SessionCalendar" ? o with { Availability = MarketSourceAvailability.Unavailable } : o).ToArray();
        if (scenario == "feed-unhealthy") observations = observations.Select(o => o.SourceId == "FeedHealth" ? o with { Value = 0 } : o).ToArray();
        if (scenario == "feed") observations = observations.Select(o => o.SourceId == "FeedHealth" ? o with { Availability = MarketSourceAvailability.Unavailable } : o).ToArray();
        return (c,new MarketConditionAssessmentSnapshot { SnapshotId = Guid.NewGuid(), MarketProfileId = c.MarketProfileId, InstrumentRoot = "ES",
            TargetHorizon = TimeFrameType.Daily, ReferenceInstrumentId = scenario == "wrong-contract" ? "ES20270319" : contractId, EvaluatedAtUtc = at,
            Quote = new(5000, scenario == "crossed" ? 4999.75m : scenario == "wide" ? 5001 : scenario == "max-spread" ? 5000.50m : 5000.25m,scenario == "shallow" ? 1 : scenario == "zero-depth" ? 0 : 10,scenario == "zero-depth" ? 0 : 10),
            SessionState = scenario == "closed" ? MarketSessionStatus.Closed : MarketSessionStatus.Open, EventContext = scenario == "blackout" ? AssessmentEventContext.Elevated : AssessmentEventContext.Clear,
            Observations = observations, CalendarEvidence = new() { CheckedAtUtc = at, CoverageConfirmed = scenario != "calendar", ValidUntilUtc = at.AddMinutes(30) } }.Seal());
    }
}
