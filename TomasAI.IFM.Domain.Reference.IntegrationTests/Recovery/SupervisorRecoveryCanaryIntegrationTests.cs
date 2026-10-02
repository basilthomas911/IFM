using TomasAI.IFM.Domain.Reference.IntegrationTests;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests.Recovery;

[Collection(ReferenceIntegrationInfrastructureCollection.Name)]
public sealed class SupervisorRecoveryCanaryIntegrationTests(
    ReferenceIntegrationInfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Isolated_NATS_command_and_JetStream_event_reach_generation_matched_Supervisor_projector()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var correlationId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var valueDate = new DateOnly(2026, 9, 30);

        var result = await infrastructure.RecoveryCanaryProbe.ProbeAsync(correlationId,
            generationId, valueDate, "GLBX.MDP3", TimeSpan.FromSeconds(20), deadline.Token);

        Assert.True(result.Qualified, result.Detail);
        Assert.Equal(correlationId, result.CorrelationId);
        Assert.Equal(generationId, result.GenerationId);
        Assert.NotNull(result.ProjectedUtc);
        Assert.Equal(RecoveryProofScope.SupervisorControlPlane, result.ProofScope);
        Assert.False(result.QualifiesMarketDataAdmission);
    }
}
