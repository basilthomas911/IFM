using System;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Collection(MarketConditionConfigurationDbCollection.Name)]
public sealed class StrategyRiskParameterSetStorageTests(MarketConditionConfigurationDbFixture fixture)
{
    [Fact]
    public async Task Draft_roundtrips_and_published_version_cannot_be_modified()
    {
        var policy = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault() with { ParameterSetId = Guid.NewGuid() };
        await fixture.Context.InsertStrategyPositionRiskDraftAsync(policy, "Isolated position-risk fixture", "risk-tests");
        var stored = await fixture.Context.GetStrategyPositionRiskVersionAsync(policy.ParameterSetId, policy.Version);
        stored.Should().NotBeNull();
        stored!.ParameterSet.Should().BeEquivalentTo(policy);
        stored.PayloadSha256.Should().Be(policy.Hash());
        stored.Status.Should().Be(ConfigurationParameterSetStatus.Draft);
        await fixture.Context.PublishAsync(StrategyParameterSetKind.StrategyPositionRisk,
            policy.ParameterSetId, policy.Version, DateTime.UtcNow.AddSeconds(-1));
        stored = await fixture.Context.GetStrategyPositionRiskVersionAsync(policy.ParameterSetId, policy.Version);
        stored!.Status.Should().Be(ConfigurationParameterSetStatus.Published);
        var edited = policy with { IronCondor = policy.IronCondor! with { DailyLossLimit = 800 } };
        Func<Task> overwrite = () => fixture.Context.InsertStrategyPositionRiskDraftAsync(edited, "overwrite", "risk-tests");
        await overwrite.Should().ThrowAsync<Exception>();
        var unchanged = await fixture.Context.GetStrategyPositionRiskVersionAsync(policy.ParameterSetId, policy.Version);
        unchanged!.PayloadSha256.Should().Be(policy.Hash());
    }
}
