using System;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using Xunit;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Collection(MarketConditionConfigurationDbCollection.Name)]
[Trait("Gate", "MC-R03")]
public sealed class MarketConditionAssessmentConfigurationTests(MarketConditionConfigurationDbFixture fixture)
{
    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public async Task Published_profile_resolves_only_its_market_and_horizon_and_retirement_preserves_exact_version(TimeFrameType horizon)
    {
        var p = Profile(horizon); var at = DateTime.UtcNow;
        await fixture.Context.InsertMarketConditionAssessmentDraftAsync(p,"MC-R03 verification","MC-R03");
        (await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(at,p.MarketProfileId,"ES",horizon)).Should().BeNull();
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketConditionAssessment,p.ParameterSetId,p.Version,at.AddMinutes(-1));
        var resolved = await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(at,p.MarketProfileId,"ES",horizon);
        resolved!.ParameterSet.Should().BeEquivalentTo(p);
        resolved.PayloadSha256.Should().Be(MarketConditionAssessmentHash.Parameters(p));
        (await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(at,p.MarketProfileId+"other","ES",horizon)).Should().BeNull();
        var other = horizon == TimeFrameType.Daily ? TimeFrameType.Weekly : TimeFrameType.Daily;
        (await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(at,p.MarketProfileId,"ES",other)).Should().BeNull();
        await fixture.Context.RetireAsync(StrategyParameterSetKind.MarketConditionAssessment,p.ParameterSetId,p.Version,at);
        (await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(at.AddSeconds(1),p.MarketProfileId,"ES",horizon)).Should().BeNull();
        (await fixture.Context.GetMarketConditionAssessmentAsync(p.ParameterSetId,p.Version))!.PayloadSha256.Should().Be(resolved.PayloadSha256);
    }
    [Fact]
    public async Task Overlapping_published_versions_are_rejected_instead_of_selecting_arbitrarily()
    {
        var p = Profile(TimeFrameType.Daily); var at = DateTime.UtcNow;
        foreach (var version in new[] {p,p with { Version = 2 }})
        {
            await fixture.Context.InsertMarketConditionAssessmentDraftAsync(version,"MC-R03 ambiguity","MC-R03");
            await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketConditionAssessment,version.ParameterSetId,version.Version,at.AddMinutes(-1));
        }
        Func<Task> resolve = async () => await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(at,p.MarketProfileId,"ES",p.TargetHorizon);
        await resolve.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ambiguous*");
    }
    [Theory]
    [InlineData("market_profile_id='other'")] [InlineData("instrument_root='VX'")] [InlineData("target_horizon=999")]
    [InlineData("payload_sha256=repeat('0',64)")] [InlineData("retired_at_utc=now(), status=2")]
    public async Task Draft_metadata_and_invalid_lifecycle_mutations_are_rejected(string mutation)
    {
        var p = Profile(TimeFrameType.Weekly);
        await fixture.Context.InsertMarketConditionAssessmentDraftAsync(p,"MC-R03 immutability","MC-R03");
        Func<Task> mutate = async () => await fixture.Repository.Use("MC-R03.Mutation."+mutation,
            "UPDATE reference_configuration.market_condition_assessment_parameter_set SET "+mutation+" WHERE parameter_set_id=$1 AND version=$2;")
            .SetParameters(new Key(p.ParameterSetId,p.Version)).ExecuteCommandAsync();
        await mutate.Should().ThrowAsync<StorageException>();
    }
    [Fact]
    public async Task Future_publication_is_not_effective_and_delete_is_forbidden()
    {
        var p = Profile(TimeFrameType.Monthly); var now = DateTime.UtcNow;
        await fixture.Context.InsertMarketConditionAssessmentDraftAsync(p,"MC-R03 future","MC-R03");
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketConditionAssessment,p.ParameterSetId,p.Version,now.AddHours(1));
        (await fixture.Context.ResolveEffectiveMarketConditionAssessmentAsync(now,p.MarketProfileId,"ES",p.TargetHorizon)).Should().BeNull();
        Func<Task> delete = async () => await fixture.Repository.Use("MC-R03.Delete","DELETE FROM reference_configuration.market_condition_assessment_parameter_set WHERE parameter_set_id=$1 AND version=$2;")
            .SetParameters(new Key(p.ParameterSetId,p.Version)).ExecuteCommandAsync();
        await delete.Should().ThrowAsync<StorageException>();
    }
    static MarketConditionAssessmentParameterSet Profile(TimeFrameType horizon) => MarketConditionAssessmentParameterSet.CreateDefault("MC-R03-"+Guid.NewGuid().ToString("N"),horizon,Guid.NewGuid(),Guid.NewGuid(),1);
    readonly record struct Key(Guid Id,int Version) : IBindValue { public object Bind() => Values(Uuid(Id),Integer(Version)); }
}
