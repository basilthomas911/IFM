using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterSetModelTests
{
    [Fact] public void Seed_contains_69_unique_rows_and_preserves_dependency_defaults()
    {
        var seed=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
        seed.SignalRequirements.Should().HaveCount(69);
        seed.SignalRequirements!.Count(x=>x.IsRequired).Should().Be(35);
        seed.SignalRequirements.Select(x=>(x.Metric,x.TimeFrame)).Distinct().Should().HaveCount(69);
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed),2).Should().BeEmpty();
    }
    [Fact] public void Seed_row_ids_are_stable_for_the_same_set()
    {
        var id=Guid.NewGuid();
        RegimeDiscoveryParameterModel.CreateSeed(id).SignalRequirements.Should()
            .BeEquivalentTo(RegimeDiscoveryParameterModel.CreateSeed(id).SignalRequirements);
    }
    [Fact] public void Optional_evidence_can_be_disabled_without_changing_required_dependencies()
    {
        var seed=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
        seed=seed with { SignalRequirements=seed.SignalRequirements!.Select(x=>x with {Enabled=x.IsRequired}).ToArray() };
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed),2).Should().BeEmpty();
        seed.SignalRequirements!.Count(x=>x.Enabled).Should().Be(35);
    }
    [Fact] public void Mandatory_ema_cannot_be_removed_or_marked_optional()
    {
        var seed=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
        seed=seed with { SignalRequirements=seed.SignalRequirements!.Select(x=>
            x.Metric==RegimeDiscoverySignalMetric.Ema200&&x.TimeFrame==TimeFrameType.FifteenMinutes
            ?x with {IsRequired=false}:x).ToArray() };
        new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed),2)
            .Should().Contain(x=>x.Code=="SIGNAL.DEPENDENCY_MISSING");
    }
    [Fact] public void Canonical_hash_ignores_object_order_but_preserves_array_order()
    {
        ParameterCanonicalPayloadModel.Hash("{\"b\":2,\"a\":1}").Should().Be(ParameterCanonicalPayloadModel.Hash("{\"a\":1,\"b\":2}"));
        ParameterCanonicalPayloadModel.Hash("{\"a\":[1,2]}").Should().NotBe(ParameterCanonicalPayloadModel.Hash("{\"a\":[2,1]}"));
    }
    [Fact] public void Duplicate_json_properties_are_rejected()
    {
        Action action=()=>ParameterCanonicalPayloadModel.Hash("{\"a\":1,\"a\":2}");
        action.Should().Throw<ArgumentException>().WithMessage("PARAM.DUPLICATE_PROPERTY");
    }
    [Fact] public void Legacy_payload_does_not_gain_a_null_signal_property()
    {
        var legacy=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(),Guid.NewGuid(),TimeFrameType.Daily);
        JsonSerializer.Serialize(legacy).Should().NotContain("SignalRequirements");
    }
    [Fact] public void Stale_revision_does_not_allocate_a_version()
    {
        Action action=()=>ParameterSetLifecycleModel.NextVersion(3,2,[1,2,3]);
        action.Should().Throw<InvalidOperationException>().WithMessage("PARAM.REVISION_CONFLICT");
    }
    [Fact] public void New_draft_payload_identity_and_version_match_the_allocated_reference()
    {
        var id=Guid.NewGuid();
        var descriptor=new RegimeDiscoveryParameterModel();
        var create=new CreateParameterSetCommand { EntityId=new(id), Name="Daily", SchemaVersion=2,
            PayloadJson=descriptor.CreateDraftPayload(Guid.NewGuid()) };
        var first=ParameterMutationModel.Decide(create,0,new Dictionary<int,ParameterSetVersion>(),DateTime.UtcNow);
        var save=new SaveParameterDraftCommand {EntityId=new(id),ExpectedRevision=1,Name="Daily",SchemaVersion=2,PayloadJson=first.PayloadJson};
        var second=ParameterMutationModel.Decide(save,1,new Dictionary<int,ParameterSetVersion>{{1,first}},DateTime.UtcNow);
        using var payload=JsonDocument.Parse(second.PayloadJson);
        payload.RootElement.GetProperty("ParameterSetId").GetGuid().Should().Be(id);
        payload.RootElement.GetProperty("Version").GetInt32().Should().Be(2);
        second.Reference.PayloadSha256.Should().Be(ParameterCanonicalPayloadModel.Hash(second.PayloadJson));
        first.Reference.Version.Should().Be(1);
        JsonDocument.Parse(first.PayloadJson).RootElement.GetProperty("Version").GetInt32().Should().Be(1);
    }
    [Fact] public void Rename_changes_metadata_without_rewriting_payload_or_version()
    {
        var id=Guid.NewGuid();var create=new CreateParameterSetCommand{EntityId=new(id),Name="Original",SchemaVersion=2,PayloadJson=new RegimeDiscoveryParameterModel().CreateDraftPayload(id)};
        var first=ParameterMutationModel.Decide(create,0,new Dictionary<int,ParameterSetVersion>(),DateTime.UtcNow);
        var rename=new RenameParameterSetCommand{EntityId=new(id),ExpectedRevision=1,Version=1,Name="Renamed",Description="Updated description"};
        var renamed=ParameterMutationModel.Decide(rename,1,new Dictionary<int,ParameterSetVersion>{{1,first}},DateTime.UtcNow);
        renamed.Name.Should().Be("Renamed");renamed.Description.Should().Be("Updated description");
        renamed.Reference.Should().Be(first.Reference);renamed.PayloadJson.Should().Be(first.PayloadJson);
        renamed.CreatedAtUtc.Should().Be(first.CreatedAtUtc);renamed.Status.Should().Be(first.Status);
    }
    [Fact] public void Retirement_requires_an_authoritative_usage_decision_and_rejects_assigned_versions()
    {
        var id=Guid.NewGuid();var create=new CreateParameterSetCommand{EntityId=new(id),Name="Daily",SchemaVersion=2,PayloadJson=new RegimeDiscoveryParameterModel().CreateDraftPayload(id)};
        var version=ParameterMutationModel.Decide(create,0,new Dictionary<int,ParameterSetVersion>(),DateTime.UtcNow) with {Status=ParameterVersionStatus.Published,PublishedAtUtc=DateTime.UtcNow};
        var versions=new Dictionary<int,ParameterSetVersion>{{1,version}};
        var command=new RetireParameterVersionCommand{EntityId=new(id),ExpectedRevision=2,Version=1};
        Action uncheckedUsage=()=>ParameterMutationModel.Decide(command,2,versions,DateTime.UtcNow);
        uncheckedUsage.Should().Throw<InvalidOperationException>().WithMessage("PARAM.RETIRE_REQUIRES_USAGE_CHECK");
        Action assigned=()=>ParameterMutationModel.Decide(command,2,versions,DateTime.UtcNow,true);
        assigned.Should().Throw<InvalidOperationException>().WithMessage("PARAM.VERSION_IN_USE");
        var retired=ParameterMutationModel.Decide(command,2,versions,DateTime.UtcNow,false);
        retired.Status.Should().Be(ParameterVersionStatus.Retired);retired.Reference.Should().Be(version.Reference);
    }

    [Fact] public void Saving_schema_migration_appends_version_and_retains_schema_three_source()
    {
        var id=Guid.NewGuid();
        var schemaThree=RegimeDiscoveryParameterModel.CreateExplicitSeed(id) with {SchemaVersion=3};
        var create=new CreateParameterSetCommand{EntityId=new(id),Name="Schema 3",SchemaVersion=3,PayloadJson=JsonSerializer.Serialize(schemaThree)};
        var first=ParameterMutationModel.Decide(create,0,new Dictionary<int,ParameterSetVersion>(),DateTime.UtcNow);
        var firstJson=first.PayloadJson;
        var migrated=RegimeDiscoveryParameterModel.UpgradeToExplicitSet(JsonSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet>(first.PayloadJson)!);
        var save=new SaveParameterDraftCommand{EntityId=new(id),ExpectedRevision=1,Name="Schema 4",SchemaVersion=ParameterSchemaRegistry.CurrentRegimeSchemaVersion,PayloadJson=JsonSerializer.Serialize(migrated)};
        var versions=new Dictionary<int,ParameterSetVersion>{{1,first}};
        var second=ParameterMutationModel.Decide(save,1,versions,DateTime.UtcNow);
        second.Reference.Version.Should().Be(2);
        second.SchemaVersion.Should().Be(ParameterSchemaRegistry.CurrentRegimeSchemaVersion);
        JsonDocument.Parse(second.PayloadJson).RootElement.GetProperty("Version").GetInt32().Should().Be(2);
        versions[1].SchemaVersion.Should().Be(3);
        versions[1].PayloadJson.Should().Be(firstJson);
    }}
