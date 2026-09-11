using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public sealed partial class ConfigurationDbContext
{
 public async Task<ParameterComponentSummary[]> ReadParameterComponentsAsync(CancellationToken token=default)
 {
  await using var connection=await OpenCatalogAsync(token);await using var command=connection.CreateCommand();
  command.CommandText="""
  SELECT a.code,a.name,c.code,c.name,array_agg(s.schema_version ORDER BY s.schema_version)
  FROM reference_configuration.parameter_area a JOIN reference_configuration.parameter_component c ON c.area_id=a.area_id
  JOIN reference_configuration.parameter_schema_version s ON s.component_code=c.code
  WHERE a.enabled AND c.enabled GROUP BY a.code,a.name,c.code,c.name ORDER BY a.name,c.name;
  """;
  await using var reader=await command.ExecuteReaderAsync(token);var result=new List<ParameterComponentSummary>();
  while(await reader.ReadAsync(token))result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetFieldValue<int[]>(4),reader.GetString(2)==ParameterSchemaRegistry.RegimeComponent));
  return result.ToArray();
 }
 public async Task<ParameterSchemaDefinition?> ReadParameterSchemaAsync(string componentCode,int version,CancellationToken token=default)
 {
  await using var connection=await OpenCatalogAsync(token);await using var command=connection.CreateCommand();
  command.CommandText="SELECT codec,schema_sha256,schema_json::text FROM reference_configuration.parameter_schema_version WHERE component_code=$1 AND schema_version=$2";
  command.Parameters.AddWithValue(componentCode);command.Parameters.AddWithValue(version);
  await using var reader=await command.ExecuteReaderAsync(token);
  if(!await reader.ReadAsync(token))return null;
  var registered=ParameterSchemaRegistry.Default.Definitions.SingleOrDefault(x=>x.ComponentCode==componentCode&&x.Version==version);
  if(registered is null)return new(componentCode,version,reader.GetString(0),reader.GetString(2),reader.GetString(1));
  if(registered.Codec!=reader.GetString(0)||registered.SchemaSha256!=reader.GetString(1)||!System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(registered.JsonSchema),System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(2))))throw new InvalidDataException("PARAM.REGISTRY_SCHEMA_MISMATCH");
  return registered;
 }
}
