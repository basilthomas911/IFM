using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

public sealed class MarketConditionAssessmentDefaultProvisionerTests
{
    static readonly TimeFrameType[] Horizons =
        [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly];

    [Fact]
    public async Task Ensure_publishes_three_bound_profiles_once_and_is_idempotent()
    {
        var at = new DateTime(2026, 9, 9, 20, 0, 0, DateTimeKind.Utc);
        var configuration = Substitute.For<IConfigurationDbContext>();
        var published = new Dictionary<TimeFrameType, ResolvedMarketConditionAssessmentParameterSet>();
        var drafts = new Dictionary<Guid, MarketConditionAssessmentParameterSet>();

        configuration.ResolveEffectiveRegimeDiscoveryAsync(
                Arg.Any<DateTime>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<ResolvedRegimeDiscoveryParameterSet?>(Regime((TimeFrameType)call[1], at)));
        configuration.ResolveEffectiveMarketConditionAssessmentAsync(
                Arg.Any<DateTime>(), "ES.Standard", "ES", Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(
                published.GetValueOrDefault((TimeFrameType)call[3])));
        configuration.GetMarketConditionAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResolvedMarketConditionAssessmentParameterSet?>(null));
        configuration.InsertMarketConditionAssessmentDraftAsync(
                Arg.Any<MarketConditionAssessmentParameterSet>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var profile = (MarketConditionAssessmentParameterSet)call[0];
                drafts.Add(profile.ParameterSetId, profile);
                return Task.CompletedTask;
            });
        configuration.PublishAsync(
                StrategyParameterSetKind.MarketConditionAssessment, Arg.Any<Guid>(), 1, at,
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var profile = drafts[(Guid)call[1]];
                published.Add(profile.TargetHorizon, new(
                    profile,
                    MarketConditionAssessmentHash.Parameters(profile),
                    at,
                    ConfigurationParameterSetStatus.Published));
                return Task.CompletedTask;
            });

        var provisioner = new MarketConditionAssessmentDefaultProvisioner(configuration);

        var first = await provisioner.EnsureAsync("ES.Standard", at, "test");
        var second = await provisioner.EnsureAsync("ES.Standard", at, "test");

        first.Should().Be(new MarketConditionAssessmentDefaultProvisioningResult(0, 3, 0));
        second.Should().Be(new MarketConditionAssessmentDefaultProvisioningResult(3, 0, 0));
        published.Keys.Should().BeEquivalentTo(Horizons);
        foreach (var horizon in Horizons)
        {
            var expectedRegime = Regime(horizon, at).ParameterSet;
            published[horizon].ParameterSet.HorizonProfile.RegimeProfileId.Should().Be(expectedRegime.ParameterSetId);
            published[horizon].ParameterSet.HorizonProfile.RegimeProfileVersion.Should().Be(expectedRegime.Version);
        }
        await configuration.Received(3).InsertMarketConditionAssessmentDraftAsync(
            Arg.Any<MarketConditionAssessmentParameterSet>(), Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
        await configuration.Received(3).PublishAsync(
            StrategyParameterSetKind.MarketConditionAssessment, Arg.Any<Guid>(), 1, at, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ensure_does_not_overwrite_an_incompatible_authored_profile()
    {
        var at = new DateTime(2026, 9, 9, 20, 0, 0, DateTimeKind.Utc);
        var configuration = Substitute.For<IConfigurationDbContext>();
        var regime = Regime(TimeFrameType.Daily, at);
        var authored = MarketConditionAssessmentParameterSet.CreateDefault(
            "ES.Standard", TimeFrameType.Daily, Guid.NewGuid(), Guid.NewGuid(), 1);
        configuration.ResolveEffectiveRegimeDiscoveryAsync(
                at, TimeFrameType.Daily, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResolvedRegimeDiscoveryParameterSet?>(regime));
        configuration.ResolveEffectiveMarketConditionAssessmentAsync(
                at, "ES.Standard", "ES", TimeFrameType.Daily, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResolvedMarketConditionAssessmentParameterSet?>(new(
                authored,
                MarketConditionAssessmentHash.Parameters(authored),
                at.AddDays(-1),
                ConfigurationParameterSetStatus.Published)));

        var act = () => new MarketConditionAssessmentDefaultProvisioner(configuration)
            .EnsureAsync("ES.Standard", at, "test");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was authored outside Development default provisioning*");
        await configuration.DidNotReceiveWithAnyArgs().InsertMarketConditionAssessmentDraftAsync(
            default!, default!, default!, default);
        await configuration.DidNotReceiveWithAnyArgs().PublishAsync(default, default, default, default, default);
    }

    static ResolvedRegimeDiscoveryParameterSet Regime(TimeFrameType horizon, DateTime at)
    {
        var horizonByte = checked((byte)horizon);
        var parameterIdBytes = new byte[16];
        parameterIdBytes[0] = horizonByte;
        var strategyIdBytes = new byte[16];
        strategyIdBytes[0] = horizonByte;
        strategyIdBytes[1] = 1;
        var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
            new Guid(parameterIdBytes), new Guid(strategyIdBytes), horizon);
        return new(parameterSet, "{}", $"regime-{horizon}", at.AddDays(-1));
    }
}
