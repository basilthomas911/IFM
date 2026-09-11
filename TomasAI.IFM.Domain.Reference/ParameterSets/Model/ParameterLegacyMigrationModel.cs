using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
public static class ParameterLegacyMigrationModel
{
 public const string RegimeKind="regime_discovery_parameter_set";
 public const string LegacyCodec="regime-typed-json-v1";
 public static Guid TargetId(ParameterLegacyReference source)=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"parameter-migration:{source.Kind}:{source.SetId:N}:{source.Version}")).AsSpan(0,16));
 public static string Expand(ParameterLegacyVersion source)
 {
  if(source.Reference.Kind!=RegimeKind||source.Reference.Codec!=LegacyCodec)throw new ArgumentException("PARAM.LEGACY_KIND_UNSUPPORTED");
  var value=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(source.PayloadJson)??throw new InvalidDataException("PARAM.LEGACY_INVALID");
  if(value.ParameterSetId!=source.Reference.SetId||value.Version!=source.Reference.Version||value.SchemaVersion!=source.SchemaVersion||
    !string.Equals(RegimeDiscoveryParameterPayload.ComputeSha256(value),source.Reference.PayloadSha256,StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("PARAM.LEGACY_HASH_MISMATCH");
  if(!ParameterSchemaRegistry.Default.CanEditLosslessly(ParameterSchemaRegistry.RegimeComponent,value.SchemaVersion,source.PayloadJson))
    throw new InvalidDataException("PARAM.LEGACY_UNSUPPORTED_FIELDS");
  // Preserve the frozen calculation settings and optionality. Schema 1 expands its own exact horizon, never seed defaults.
  if(value.SchemaVersion==1)value=value with {SchemaVersion=2,SignalRequirements=RegimeDiscoveryParameterModel.Defaults(value)};
  value=value with {ParameterSetId=TargetId(source.Reference),Version=1};
  return ParameterCanonicalPayloadModel.Canonicalize(JsonSerializer.Serialize(value));
 }
}
