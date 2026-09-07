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
