using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Development;

public sealed class RegimeDiscoveryDefaultProvisionerTests
{
    [Fact]
    public async Task Ensure_publishes_each_missing_horizon_once_and_is_idempotent()
    {
        var at = new DateTime(2026, 9, 27, 20, 0, 0, DateTimeKind.Utc);
        var configuration = Substitute.For<IConfigurationDbContext>();
        var published = new Dictionary<TimeFrameType, ResolvedRegimeDiscoveryParameterSet>();
        var drafts = new Dictionary<Guid, RegimeDiscoveryParameterSet>();
        configuration.GetEffectiveRegimeDiscoveryAsync(
                at, Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(published.GetValueOrDefault(call.ArgAt<TimeFrameType>(1))));
        configuration.GetRegimeDiscoveryAsync(Arg.Any<Guid>(), 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResolvedRegimeDiscoveryParameterSet?>(null));
        configuration.InsertRegimeDiscoveryDraftAsync(
                Arg.Any<RegimeDiscoveryParameterSet>(), Arg.Any<string>(), "test", Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var value = call.ArgAt<RegimeDiscoveryParameterSet>(0);
                drafts.Add(value.ParameterSetId, value);
                return Task.CompletedTask;
            });
        configuration.PublishAsync(
                StrategyParameterSetKind.RegimeDiscovery, Arg.Any<Guid>(), 1, at, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var value = drafts[call.ArgAt<Guid>(1)];
                published.Add(value.TargetHorizon, new(
                    value,
                    RegimeDiscoveryParameterPayload.Serialize(value),
                    RegimeDiscoveryParameterPayload.ComputeSha256(value),
                    at));
                return Task.CompletedTask;
            });

        var provisioner = new RegimeDiscoveryDefaultProvisioner(configuration);

        var first = await provisioner.EnsureAsync(at, "test");
        var second = await provisioner.EnsureAsync(at, "test");

        first.Should().Be(new RegimeDiscoveryDefaultProvisioningResult(0, 3));
        second.Should().Be(new RegimeDiscoveryDefaultProvisioningResult(3, 0));
        published.Keys.Should().BeEquivalentTo(
            [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly]);
        await configuration.Received(3).InsertRegimeDiscoveryDraftAsync(
            Arg.Any<RegimeDiscoveryParameterSet>(), Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
        await configuration.Received(3).PublishAsync(
            StrategyParameterSetKind.RegimeDiscovery, Arg.Any<Guid>(), 1, at, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ensure_preserves_an_effective_authored_profile()
    {
        var at = new DateTime(2026, 9, 27, 20, 0, 0, DateTimeKind.Utc);
        var configuration = Substitute.For<IConfigurationDbContext>();
        var authored = RegimeDiscoveryParameterSet.CreateDefault(
            Guid.NewGuid(), Guid.NewGuid(), TimeFrameType.Daily);
        configuration.GetEffectiveRegimeDiscoveryAsync(
                at, TimeFrameType.Daily, Arg.Any<CancellationToken>())
            .Returns(new ResolvedRegimeDiscoveryParameterSet(
                authored,
                RegimeDiscoveryParameterPayload.Serialize(authored),
                RegimeDiscoveryParameterPayload.ComputeSha256(authored),
                at.AddDays(-1)));

        var result = await new RegimeDiscoveryDefaultProvisioner(configuration).EnsureAsync(at, "test");

        result.ExistingProfiles.Should().Be(1);
        await configuration.DidNotReceive().InsertRegimeDiscoveryDraftAsync(
            Arg.Is<RegimeDiscoveryParameterSet>(value => value.TargetHorizon == TimeFrameType.Daily),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
