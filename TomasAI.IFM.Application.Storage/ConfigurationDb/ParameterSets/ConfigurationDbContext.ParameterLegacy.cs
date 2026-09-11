using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public sealed partial class ConfigurationDbContext
{
 public async Task<ParameterLegacyVersion[]> ReadLegacyParameterVersionsAsync(Guid? setId=null,int version=0,int offset=0,CancellationToken token=default)
 {
  if(offset<0||version<0)throw new ArgumentOutOfRangeException(nameof(offset));
  await using var connection=await OpenCatalogAsync(token);await using var command=connection.CreateCommand();
  command.CommandText="SELECT parameter_set_id,version,schema_version,payload_json::text,payload_sha256,description,status FROM reference_configuration.regime_discovery_parameter_set WHERE ($1::uuid IS NULL OR parameter_set_id=$1) AND ($2=0 OR version=$2) ORDER BY parameter_set_id,version LIMIT 100 OFFSET $3";
  command.Parameters.Add(new Npgsql.NpgsqlParameter{NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)setId??DBNull.Value});command.Parameters.AddWithValue(version);command.Parameters.AddWithValue(offset);
  await using var reader=await command.ExecuteReaderAsync(token);var rows=new List<ParameterLegacyVersion>();
  while(await reader.ReadAsync(token))rows.Add(new(new("regime_discovery_parameter_set",reader.GetGuid(0),reader.GetInt32(1),reader.GetString(4),"regime-typed-json-v1"),reader.GetInt16(2),reader.GetString(3),reader.GetString(5),reader.GetInt16(6)));
  return rows.ToArray();
 }
}
