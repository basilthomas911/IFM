using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterLegacyMigrationTests
{
 [Fact]public void Legacy_expansion_preserves_frozen_settings_and_disambiguates_kinds()
 {
  var source=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid()) with {Version=7};
  var json=RegimeDiscoveryParameterPayload.Serialize(source);
  var reference=new ParameterLegacyReference(ParameterLegacyMigrationModel.RegimeKind,source.ParameterSetId,7,RegimeDiscoveryParameterPayload.ComputeSha256(source),ParameterLegacyMigrationModel.LegacyCodec);
  var stored=new ParameterLegacyVersion(reference,2,json,"Old settings",1);
  var migrated=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(ParameterLegacyMigrationModel.Expand(stored))!;
  (migrated with {ParameterSetId=source.ParameterSetId,Version=source.Version}).Should().BeEquivalentTo(source);
  ParameterLegacyMigrationModel.TargetId(reference).Should().NotBe(ParameterLegacyMigrationModel.TargetId(reference with {Kind="another-table"}));
  Action badHash=()=>ParameterLegacyMigrationModel.Expand(stored with {Reference=reference with {PayloadSha256=new string('0',64)}});
  badHash.Should().Throw<InvalidDataException>().WithMessage("PARAM.LEGACY_HASH_MISMATCH");
  RegimeDiscoveryParameterPayload.ComputeSha256(source).Should().Be(reference.PayloadSha256);
 }
 [Fact]public void Unsupported_legacy_optional_producers_are_visible_without_stopping_other_demands()
 {
  var value=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());var json=JsonSerializer.Serialize(value);
  var version=new ParameterSetVersion(new(value.ParameterSetId,1,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(json)),"Legacy","",2,ParameterVersionStatus.Published,json,DateTime.UtcNow,"test");
  var scope=WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,value.TargetHorizon);
  var assignment=ParameterAssignmentModel.Assign(scope,version,null,0,DateTime.UtcNow,"test");
  var snapshot=new ParameterStartupSnapshotModel(Guid.NewGuid(),[assignment],new Dictionary<ParameterVersionRef,ParameterSetVersion>{{version.Reference,version}});
  var plan=SignalStartupPlanModel.Create(snapshot,[]);
  plan.Steps.Should().NotBeEmpty();plan.Issues.Should().NotBeEmpty();
  plan.Issues.Should().Contain(x=>x.Contains("VixLevel"));
  SignalStartupPlanModel.Create(snapshot,[]).Fingerprint.Should().Be(plan.Fingerprint);
 }
}
