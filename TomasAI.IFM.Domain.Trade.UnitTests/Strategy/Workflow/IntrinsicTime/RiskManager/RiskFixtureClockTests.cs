using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

public sealed class RiskFixtureClockTests
{
    [Fact]
    public async Task Explicit_seed_clock_never_places_risk_before_its_accepted_composition_after_transport()
    {
        var seed = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);
        var request = await RiskFixture.Command(atUtc: seed);
        var codec = new MessagePackBinarySerializer();
        var restored = codec.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteRiskManagementPipelineCommand>(codec.Serialize(request)! )!;
        Assert.True(restored.EvaluatedAtUtc >= restored.CompositionResult.ReadCompositionResult().Candidate!.EvaluatedAtUtc);
        Assert.Equal(RiskAssessmentOutcome.Approved, new RiskEvaluator().Calculate(restored).Outcome);
    }
}
