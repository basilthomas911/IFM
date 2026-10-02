using TomasAI.IFM.Application.Api.Server;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class FinancialJetStreamPolicyTests
{
    [Theory]
    [InlineData("IFM_FuturesTickDataEventProjector_PROCESS", true)]
    [InlineData("IFM_FuturesBarDataEventProjector_REPLAY", true)]
    [InlineData("IFM_FuturesItiSignalEventProjector_REPLAY", true)]
    [InlineData("IFM_BrokerOrderEventProjector_PROCESS", false)]
    [InlineData("IFM_OrderExecutionEventProjector_REPLAY", false)]
    [InlineData("IFM_EmulatorExecutionProjector_REPLAY", false)]
    [InlineData("IFM_GeneralLedgerProjector_REPLAY", false)]
    [InlineData("IFM_PortfolioFundEventProjector_PROCESS", false)]
    [InlineData("IFM_integration_probe_PROCESS", false)]
    public void Startup_purge_targets_only_nonfinancial_projector_streams(
        string streamName, bool expected)
        => Assert.Equal(expected, FinancialJetStreamPolicy.ShouldPurgeProjectorStream(streamName));
}
