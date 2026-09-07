using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionAssessmentContractTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public void One_matching_accepted_regime_round_trips_with_frozen_parameters(TimeFrameType horizon)
    {
        var c = AssessmentFixture.Command(horizon);
        var restored = MessagePackSerializer.Deserialize<ExecuteMarketConditionAssessmentCommand>(MessagePackSerializer.Serialize(c));
        MarketConditionAssessmentContracts.ValidateRequest(restored).TargetHorizon.Should().Be(horizon);
        restored.Fingerprint().Should().Be(c.Fingerprint());
        restored.EntityId.Format().Should().Contain("MarketCondition.AssessmentV2");
    }

    [Theory]
    [InlineData("horizon")] [InlineData("workflow")] [InlineData("hash")] [InlineData("unaccepted")]
    [InlineData("profile")] [InlineData("trigger")] [InlineData("legacy")] [InlineData("subject")]
    public void Cross_workflow_timeframe_profile_and_legacy_substitution_are_rejected(string change)
    {
        var c = AssessmentFixture.Command(TimeFrameType.Weekly);
        c = change switch
        {
            "horizon" => c with { TargetHorizon = TimeFrameType.Daily },
            "workflow" => c with { WorkflowView = c.WorkflowView with { WorkflowId = StrategyWorkflowId.New(TimeProvider.System) } },
            "hash" => c with { RegimePayloadSha256 = new string('0', 64) },
            "unaccepted" => c with { WorkflowView = c.WorkflowView with { RegimeDiscovery = new() } },
            "profile" => c with { MarketProfileId = "another" },
            "trigger" => c with { TriggerEvent = c.TriggerEvent with { Id = Guid.NewGuid() } },
            "legacy" => c with { WorkflowView = c.WorkflowView with { AssessmentBinding = null } },
            _ => c with { Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, "Execute", c.EntityId.Format()) }
        };
        Action validate = () => MarketConditionAssessmentContracts.ValidateRequest(c);
        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parameter_order_is_canonical_and_collections_are_defensive()
    {
        var p = AssessmentFixture.Command().ParameterSet;
        MarketConditionAssessmentHash.Parameters(p with { Sources = p.Sources.Reverse().ToArray() }).Should().Be(MarketConditionAssessmentHash.Parameters(p));
        var sources = p.Sources; sources[0] = sources[0] with { MaximumAgeSeconds = 999 };
        p.Sources[0].MaximumAgeSeconds.Should().NotBe(999);
        typeof(MarketConditionAssessmentParameterSet).GetProperties().Select(x => x.Name)
            .Should().NotContain(x => x.Contains("Fund") || x.Contains("Family") || x.Contains("Option") || x.Contains("Broker"));
    }

    [Fact]
    public void New_request_fingerprint_detects_changed_inputs_and_legacy_stream_is_separate()
    {
        var c = AssessmentFixture.Command();
        (c with { CorrelationId = Guid.NewGuid() }).Fingerprint().Should().NotBe(c.Fingerprint());
        c.EntityId.Format().Should().NotBe(MarketConditionExecutionEntityId.Create(c.WorkflowEntityId, c.WorkflowId).Format());
        var normalized=c.ParameterSet with {MovementStressThreshold=1.50000m};
        (c with {ParameterSet=normalized,WorkflowView=c.WorkflowView with {AssessmentBinding=c.WorkflowView.AssessmentBinding! with {Parameters=normalized}}})
            .Fingerprint().Should().Be(c.Fingerprint());
    }
}

internal static class AssessmentFixture
{
    public static ExecuteMarketConditionAssessmentCommand Command(TimeFrameType horizon = TimeFrameType.Daily)
    {
        var at = new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);
        var workflowId = new StrategyWorkflowId(Guid.NewGuid());
        var itiId = FuturesItiSignalEntityId.Create("ESZ6", DateOnly.FromDateTime(at), horizon);
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(itiId);
        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Id = Guid.Parse("019917f7-1c00-7000-8000-000000000002"), EntityId = itiId, CreatedOn = at.AddSeconds(-1),
            FuturesItiSignal = new FuturesItiSignalV2ReadModel { ContractId = "ESZ6", ValueDate = itiId.ValueDate,
                TimeFrameStartValueDate = itiId.ValueDate, TimePeriod = horizon, SequenceId = 1, IntrinsicTime = at,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged, BandLevel = 1d, ReversalLevel = 0.1d,
                TradingDays = 1 }
        };
        var upstream = new RegimeDiscoveryResult
        {
            ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000003"), WorkflowId = workflowId,
            EntityId = entity, TriggerEventId = trigger.Id, TargetHorizon = horizon, ProducedAtUtc = at.AddSeconds(-2),
            OverallConfidence = 0.90m, OverallQuality = RegimeOverallQuality.High,
            Trend = new() { IsComplete = true, Direction = RegimeDirection.Up },
            Volatility = new() { IsComplete = true, Change = VolatilityRegimeChange.Stable },
            MarketStructure = new() { IsComplete = true, Classification = MarketStructureClassification.Trending,
                Direction = RegimeDirection.Up },
            Decision = new() { IsComplete = true, Direction = RegimeDirection.Up, Confidence = 0.90m,
                Quality = RegimeOverallQuality.High }
        };

        var rp = RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), horizon);
        var p = MarketConditionAssessmentParameterSet.CreateDefault("ES.Standard", horizon, Guid.NewGuid(), rp.ParameterSetId, rp.Version);
        var regime = upstream with
        {
            RegimeDiscoveryParameterSetId = rp.ParameterSetId, RegimeDiscoveryParameterSetVersion = rp.Version,
            MarketDataAsOfUtc = at.AddSeconds(-1),
            Decision = upstream.Decision with { StructureClassification = MarketStructureClassification.Trending, VolatilityChange = VolatilityRegimeChange.Stable }
        };
        var envelope = StrategyStageResultEnvelope.Create(regime.ResultId, nameof(RegimeDiscoveryResult), RegimeDiscoveryResult.CurrentSchemaVersion,
            MessagePackSerializer.Serialize(regime), regime.MarketDataAsOfUtc, regime.ProducedAtUtc);
        var binding = new MarketConditionAssessmentBinding { Parameters = p, PayloadSha256 = MarketConditionAssessmentHash.Parameters(p) };
        var v = new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = entity, WorkflowId = workflowId, Status = WorkflowStrategyMachineStatus.Started,
            CurrentStage = StrategyWorkflowStage.MarketCondition, WorkflowRevision = 2, FundId = 1,
            UpdatedAtUtc = at, TriggerEventId = trigger.Id,
            TriggerEvent = trigger, RegimeDiscoveryParameterSet = rp,
            RegimeDiscoveryParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(rp),
            ExpiresAtUtc = at.AddMinutes(1), AssessmentBinding = binding,
            RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed, CompletedAtUtc = at.AddSeconds(-1), Result = envelope }
        };
        var id = new MarketConditionAssessmentExecutionId(v.EntityId, v.WorkflowId);
        return new()
        {
            CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, ExecuteMarketConditionAssessmentCommand.Verb, id.Format()),
            WorkflowView = v, TriggerEvent = trigger, ParameterSet = p, ParameterPayloadSha256 = binding.PayloadSha256,
            MarketProfileId = p.MarketProfileId, InstrumentRoot = p.InstrumentRoot, TargetHorizon = horizon,
            InputWorkflowRevision = v.WorkflowRevision, RequestedAtUtc = at, ExpiresAtUtc = at.AddSeconds(5),
            RegimeResultEnvelope = envelope, RegimePayloadSha256 = envelope.PayloadSha256
        };
    }
}
