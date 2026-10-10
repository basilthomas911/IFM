using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Composition;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ApiDatabentoRecoveryHostActivationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void Default_disabled_does_not_require_any_host_dependency(
        bool isDevelopment, bool supervisedWorkers, bool downstreamProofAvailable)
    {
        Assert.False(ApiDatabentoRecoveryHostActivation.Validate(false,
            isDevelopment, supervisedWorkers, downstreamProofAvailable));
    }

    [Fact]
    public void Production_cannot_opt_in_even_with_every_dependency()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ApiDatabentoRecoveryHostActivation.Validate(true, false, true, true));
    }

    [Fact]
    public void Development_opt_in_requires_supervised_workers()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ApiDatabentoRecoveryHostActivation.Validate(true, true, false, true));
    }

    [Fact]
    public void Development_opt_in_requires_exact_generation_admission()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ApiDatabentoRecoveryHostActivation.Validate(true, true, true, false));
    }

    [Fact]
    public void Development_opt_in_is_valid_only_when_all_boundaries_are_composed()
    {
        Assert.True(ApiDatabentoRecoveryHostActivation.Validate(true, true, true, true));
    }
}
