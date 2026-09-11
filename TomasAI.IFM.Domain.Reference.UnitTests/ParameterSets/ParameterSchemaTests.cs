using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterSchemaTests
{
 [Fact]public void Every_supported_schema_is_registered_and_deterministic()
 {
  var registry=ParameterSchemaRegistry.Default;
  registry.Definitions.Select(x=>x.Version).Should().Equal(1,2,3,4,5);
  foreach(var schema in registry.Definitions)
  {
   using var document=JsonDocument.Parse(schema.JsonSchema);
   document.RootElement.GetProperty("properties").GetProperty("SchemaVersion").GetProperty("const").GetInt32().Should().Be(schema.Version);
   schema.SchemaSha256.Should().HaveLength(64);
  }
 }
 [Fact]public void Structural_validation_rejects_bad_types_but_preserves_unknown_fields()
 {
  var schema=ParameterSchemaRegistry.Default;var code=ParameterSchemaRegistry.RegimeComponent;
  schema.ValidateStructure(code,3,"{\"SchemaVersion\":3,\"Trend\":\"bad\"}").Should().Contain(x=>x.Path=="Payload/Trend");
  schema.ValidateStructure(code,3,"{\"SchemaVersion\":\"bad\"}").Should().NotBeEmpty();
  schema.ValidateStructure(code,3,"{\"SchemaVersion\":3,\"FutureExtension\":{\"value\":1}}").Should().BeEmpty();
 }
 [Fact]public void Typed_editing_rejects_unknown_fields_at_any_depth()
 {
  var registry=ParameterSchemaRegistry.Default;var code=ParameterSchemaRegistry.RegimeComponent;
  registry.CanEditLosslessly(code,3,"{\"SchemaVersion\":3,\"FutureField\":1}").Should().BeFalse();
  registry.CanEditLosslessly(code,3,"{\"SchemaVersion\":3,\"Trend\":{\"FutureField\":1}}").Should().BeFalse();
  registry.CanEditLosslessly(code,99,"{\"SchemaVersion\":99}").Should().BeFalse();
  registry.CanEditLosslessly(code,ParameterSchemaRegistry.CurrentRegimeSchemaVersion,JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid()))).Should().BeTrue();
 }
 [Fact]public void All_seed_shapes_pass_structural_validation()
 {
  var registry=ParameterSchemaRegistry.Default;
  registry.ValidateStructure(ParameterSchemaRegistry.RegimeComponent,2,JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid()))).Should().BeEmpty();
  registry.ValidateStructure(ParameterSchemaRegistry.RegimeComponent,ParameterSchemaRegistry.CurrentRegimeSchemaVersion,JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid()))).Should().BeEmpty();
 }
 [Fact]public void Schema_four_corrects_nullability_without_changing_legacy_definitions()
 {
  var current=ParameterSchemaRegistry.Default;
  var legacy=new ParameterSchemaRegistry(Enumerable.Range(1,3)
   .Select(version=>(ParameterSchemaRegistry.RegimeComponent,version,typeof(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet))));
  var immutableHashes=new Dictionary<int,string>
  {
   [1]="6a6d9849621c56e6f79492be5aeba1a57bd32354093140383a8a8bf27ab49f22",
   [2]="a070610f85f705786e32a39151dab37ab5b35168ecfdd5d96d6a017642d61de7",
   [3]="2822d6e23301a9a46b2b3ad34ff769f3e62a40b58483a675b6e6c59452c14cca"
  };
  foreach(var definition in legacy.Definitions)
  {
   var registered=current.Get(definition.ComponentCode,definition.Version);
   registered.JsonSchema.Should().Be(definition.JsonSchema);
   registered.SchemaSha256.Should().Be(definition.SchemaSha256);
   registered.SchemaSha256.Should().Be(immutableHashes[definition.Version]);
  }
  using var schema=JsonDocument.Parse(current.Get(ParameterSchemaRegistry.RegimeComponent,4).JsonSchema);
  var properties=schema.RootElement.GetProperty("properties");
  properties.GetProperty("Trend").TryGetProperty("anyOf",out _).Should().BeFalse();
  properties.GetProperty("SignalRequirements").GetProperty("anyOf").EnumerateArray()
   .Select(node=>node.GetProperty("type").GetString()).Should().Contain("null");
  current.ValidateStructure(ParameterSchemaRegistry.RegimeComponent,3,"{\"SchemaVersion\":3,\"Trend\":null}").Should().BeEmpty();
  current.ValidateStructure(ParameterSchemaRegistry.RegimeComponent,4,"{\"SchemaVersion\":4,\"Trend\":null}")
   .Should().Contain(issue=>issue.Path=="Payload/Trend");
 }
 [Fact]public void Null_domain_objects_remain_read_only_in_the_typed_editor()
 {
  ParameterSchemaRegistry.Default.CanEditLosslessly(ParameterSchemaRegistry.RegimeComponent,4,"{\"SchemaVersion\":4,\"Trend\":null}").Should().BeFalse();
 }

}

