using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionDecisionAndHintTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily, MarketConditionTradeType.Futures, MarketConditionHintSuitability.Preferred)]
    [InlineData(TimeFrameType.Weekly, MarketConditionTradeType.VerticalSpread, MarketConditionHintSuitability.Preferred)]
    [InlineData(TimeFrameType.Monthly, MarketConditionTradeType.IronCondor, MarketConditionHintSuitability.Eligible)]
    public void Minimum_advisory_hint_mapping_is_stable_and_extensible(TimeFrameType horizon,
        MarketConditionTradeType tradeType, MarketConditionHintSuitability suitability)
    {
        var result = new MarketConditionCalculationModel().Calculate(MarketConditionV1Tests.Healthy(horizon));

        result.SchemaVersion.Should().Be(2);
        result.OutputHints.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            TradeType = tradeType,
            TimeFrame = horizon,
            Suitability = suitability,
            IsAdvisory = true
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Hint_is_derived_after_primary_decision_and_cannot_make_a_blocked_market_tradeable()
    {
        var input = MarketConditionV1Tests.Healthy();
        input = input with
        {
            Snapshot = MarketConditionSnapshotHash.Seal(input.Snapshot with
            {
                FuturesQuote = input.Snapshot.FuturesQuote with { BidSize = 0m }
            })
        };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.NotTradeable);
        result.OutputHints.Single().Suitability.Should().Be(MarketConditionHintSuitability.Avoid);
        result.OutputHints.Single().Confidence.Should().Be(0m);
    }

    [Fact]
    public void Regime_decision_direction_is_primary_and_exact_trigger_is_corroboration()
    {
        var input = MarketConditionV1Tests.Healthy();
        input = input with
        {
            RegimeResult = input.RegimeResult with
            {
                Decision = input.RegimeResult.Decision with { Direction = RegimeDirection.Down }
            }
        };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Direction.Should().Be(MarketConditionDirection.Bearish);
        result.UpstreamAlignment.Should().Be(MarketConditionUpstreamAlignment.Conflict);
        result.PrimaryReasonCode.Should().Be(MarketConditionReasonCodes.RegimeTriggerConflict);
    }

    [Fact]
    public void Expanded_decision_language_overrides_legacy_specialist_fallbacks()
    {
        var input = MarketConditionV1Tests.Healthy();
        input = input with
        {
            RegimeResult = input.RegimeResult with
            {
                Decision = input.RegimeResult.Decision with
                {
                    Direction = RegimeDirection.Neutral,
                    TrendPhase = TrendRegimePhase.RangeBound,
                    VolatilityChange = VolatilityRegimeChange.Contracting,
                    StructureClassification = MarketStructureClassification.Compressing,
                    TermStructure = VxTermStructureRegime.Contango
                }
            }
        };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Direction.Should().Be(MarketConditionDirection.Neutral);
        result.Phase.Should().Be(MarketConditionPhase.Confirmed);
        result.VolatilityBehavior.Should().Be(MarketConditionVolatilityBehavior.Contracting);
        result.ConditionType.Should().Be(MarketConditionType.VolatilityContraction);
        result.EvidenceItems.Should().Contain(x => x.FeatureCode == "RD.TermStructure" &&
            x.ObservedText == nameof(VxTermStructureRegime.Contango));
    }

    [Fact]
    public void Decision_conviction_agreement_and_strength_materially_affect_scoring()
    {
        var baseline = WithMinimums(MarketConditionV1Tests.Healthy());
        var high = baseline with { RegimeResult = WithQuality(baseline.RegimeResult, 0.90m, 0.90m,
            0.90m, TrendRegimeStrength.Extreme) };
        var low = baseline with { RegimeResult = WithQuality(baseline.RegimeResult, 0.20m, 0.20m,
            0.20m, TrendRegimeStrength.Weak) };
        var model = new MarketConditionCalculationModel();

        var highResult = model.Calculate(high);
        var lowResult = model.Calculate(low);

        highResult.Strength.Should().BeGreaterThan(lowResult.Strength);
        highResult.Confidence.Should().BeGreaterThan(lowResult.Confidence);
    }

    [Fact]
    public void Result_schema_v2_round_trip_preserves_advisory_hints()
    {
        var result = new MarketConditionCalculationModel().Calculate(MarketConditionV1Tests.Healthy());
        var restored = MessagePackSerializer.Deserialize<MarketConditionResult>(
            MessagePackSerializer.Serialize(result));

        restored.SchemaVersion.Should().Be(MarketConditionResult.CurrentSchemaVersion);
        restored.OutputHints.Should().BeEquivalentTo(result.OutputHints);
    }

    [Fact]
    public void Schema_v1_shaped_payload_remains_readable_with_empty_hints()
    {
        var payload = MessagePackSerializer.Serialize(new LegacyMarketConditionResultV1 { SchemaVersion = 1 });

        var restored = MessagePackSerializer.Deserialize<MarketConditionResult>(payload);

        restored.SchemaVersion.Should().Be(1);
        restored.OutputHints.Should().BeEmpty();
    }

    static RegimeDiscoveryResult WithQuality(RegimeDiscoveryResult result, decimal conviction,
        decimal directionalScore, decimal agreement, TrendRegimeStrength strength) => result with
    {
        Decision = result.Decision with
        {
            DirectionalScore = directionalScore,
            RiskAdjustedConviction = conviction,
            TrendTimeFrameAgreement = agreement,
            TrendStrength = strength
        }
    };

    static MarketConditionCalculationInput WithMinimums(MarketConditionCalculationInput input)
    {
        var parameters = input.ParameterSet with
        {
            Scoring = input.ParameterSet.Scoring with { MinimumStrength = 0m, MinimumConfidence = 0m }
        };
        return input with
        {
            ParameterSet = parameters,
            WorkflowView = input.WorkflowView with
            {
                MarketConditionParameterSet = parameters,
                MarketConditionParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters)
            }
        };
    }

    [MessagePackObject(AllowPrivate = true)]
    internal sealed record LegacyMarketConditionResultV1
    {
        [Key(0)] public ushort SchemaVersion { get; init; }
        [Key(1)] public Guid ResultId { get; init; }
        [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
        [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
        [Key(4)] public int FundId { get; init; }
        [Key(5)] public string InstrumentRoot { get; init; } = string.Empty;
        [Key(6)] public TimeFrameType TargetHorizon { get; init; }
        [Key(7)] public Guid TriggerEventId { get; init; }
        [Key(8)] public long InputWorkflowRevision { get; init; }
        [Key(9)] public Guid StrategyParameterSetId { get; init; }
        [Key(10)] public int StrategyParameterSetVersion { get; init; }
        [Key(11)] public Guid MarketConditionParameterSetId { get; init; }
        [Key(12)] public int MarketConditionParameterSetVersion { get; init; }
        [Key(13)] public Guid SnapshotId { get; init; }
        [Key(14)] public string SnapshotSha256 { get; init; } = string.Empty;
        [Key(15)] public DateTime EvaluatedAtUtc { get; init; }
        [Key(16)] public DateTime ValidUntilUtc { get; init; }
        [Key(17)] public DateTime MarketDataAsOfUtc { get; init; }
        [Key(18)] public MarketTradeability Tradeability { get; init; }
        [Key(19)] public MarketConditionType ConditionType { get; init; }
        [Key(20)] public MarketConditionDirection Direction { get; init; }
        [Key(21)] public MarketConditionPhase Phase { get; init; }
        [Key(22)] public decimal Strength { get; init; }
        [Key(23)] public decimal Confidence { get; init; }
        [Key(24)] public MarketConditionVolatilityBehavior VolatilityBehavior { get; init; }
        [Key(25)] public MarketConditionLiquidityQuality LiquidityQuality { get; init; }
        [Key(26)] public MarketConditionDataQuality DataQuality { get; init; }
        [Key(27)] public MarketConditionUpstreamAlignment UpstreamAlignment { get; init; }
        [Key(28)] public MarketConditionEvidenceItem[] EvidenceItems { get; init; } = [];
        [Key(29)] public MarketConditionEvidenceItem[] ConflictingEvidenceItems { get; init; } = [];
        [Key(30)] public MarketConditionBlockingReason[] BlockingReasons { get; init; } = [];
        [Key(31)] public string PrimaryReasonCode { get; init; } = string.Empty;
        [Key(32)] public string[] Reasons { get; init; } = [];
        [Key(33)] public string SummaryText { get; init; } = string.Empty;
    }
}
