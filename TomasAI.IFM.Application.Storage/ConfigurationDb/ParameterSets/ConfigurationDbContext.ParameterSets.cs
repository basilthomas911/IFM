using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public sealed partial class ConfigurationDbContext
{
 public async Task ProjectParameterSetAsync(IParameterSetFact fact,CancellationToken token=default)
 {
  var value=JsonSerializer.Deserialize<ParameterSetVersion>(fact.VersionJson)??throw new InvalidDataException("Invalid version fact.");
  await using var connection=await OpenCatalogAsync(token);
  await using var transaction=await connection.BeginTransactionAsync(token);
  await Scalar(connection,transaction,"SELECT pg_advisory_xact_lock(hashtextextended($1,0));",token,fact.EntityId.Format());
  var receipt=await Scalar(connection,transaction,"SELECT request_sha256 FROM reference_configuration.parameter_operation WHERE operation_id=$1;",token,fact.CommandId);
  if(receipt is string prior){if(prior!=fact.RequestHash)throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");return;}
  var revision=Convert.ToInt64(await Scalar(connection,transaction,"SELECT revision FROM reference_configuration.parameter_set WHERE set_id=$1;",token,fact.EntityId.SetId)??0L);
  if(revision+1!=fact.Revision)throw new InvalidOperationException("PARAM.PROJECTION_ORDER");
  await Execute(connection,transaction,"""
  INSERT INTO reference_configuration.parameter_set(set_id,component_code,name,description,revision) VALUES($1,$2,$3,$4,$5)
  ON CONFLICT(set_id) DO UPDATE SET name=EXCLUDED.name,description=EXCLUDED.description,revision=EXCLUDED.revision;
  """,token,fact.EntityId.SetId,value.Reference.ComponentCode,value.Name,value.Description,fact.Revision);
  var hash=await Scalar(connection,transaction,"SELECT payload_sha256 FROM reference_configuration.parameter_set_version WHERE set_id=$1 AND version=$2;",token,fact.EntityId.SetId,value.Reference.Version);
  if(hash is string existing&&existing!=value.Reference.PayloadSha256)throw new InvalidOperationException("PARAM.VERSION_IMMUTABLE");
  await Execute(connection,transaction,"""
  INSERT INTO reference_configuration.parameter_set_version(set_id,version,payload_sha256,status,body)
  VALUES($1,$2,$3,$4,$5::jsonb) ON CONFLICT(set_id,version) DO UPDATE SET status=EXCLUDED.status,body=EXCLUDED.body;
  """,token,fact.EntityId.SetId,value.Reference.Version,value.Reference.PayloadSha256,(short)value.Status,fact.VersionJson);
  await Execute(connection,transaction,"INSERT INTO reference_configuration.parameter_operation(operation_id,set_id,revision,request_sha256,version) VALUES($1,$2,$3,$4,$5);",
   token,fact.CommandId,fact.EntityId.SetId,fact.Revision,fact.RequestHash,value.Reference.Version);
  if(!string.IsNullOrWhiteSpace(fact.AuditJson))
  {
   var audit=JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson)??throw new InvalidDataException("Invalid audit entry.");
   if(audit.OperationId!=fact.CommandId||audit.Revision!=fact.Revision)throw new InvalidDataException("Audit identity mismatch.");
   await Execute(connection,transaction,"INSERT INTO reference_configuration.parameter_set_audit(operation_id,entity_id,revision,body) VALUES($1,$2,$3,$4::jsonb)",token,audit.OperationId,audit.EntityId,audit.Revision,fact.AuditJson);
  }
  if(value.Reference.Version==1 && value.LegacySource is {} source)
  {
   await Execute(connection,transaction,"INSERT INTO reference_configuration.parameter_legacy_reference VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING",token,source.Kind,source.SetId,source.Version,value.Reference.SetId,value.Reference.Version,source.PayloadSha256,source.Codec);
   var same=await Scalar(connection,transaction,"SELECT generic_set_id=$4 AND legacy_sha256=$5 AND legacy_codec=$6 FROM reference_configuration.parameter_legacy_reference WHERE legacy_kind=$1 AND legacy_set_id=$2 AND legacy_version=$3",token,source.Kind,source.SetId,source.Version,value.Reference.SetId,source.PayloadSha256,source.Codec);
   if(same is not true)throw new InvalidDataException("PARAM.LEGACY_MAPPING_CONFLICT");
  }
  await transaction.CommitAsync(token);
 }
 public async Task<ParameterSetVersion[]> ReadParameterSetsAsync(string componentCode,Guid? setId=null,CancellationToken token=default,int limit=100,string afterName="",Guid? afterSetId=null,int afterVersion=0)
 {
  await using var connection=await OpenCatalogAsync(token);
  await using var command=connection.CreateCommand();
  command.CommandText="""
  SELECT v.body::text,s.name,s.description,s.revision FROM reference_configuration.parameter_set_version v JOIN reference_configuration.parameter_set s USING(set_id)
  WHERE s.component_code=$1 AND ($2::uuid IS NULL OR s.set_id=$2)
  AND ($4::uuid IS NULL OR (s.name,s.set_id,-v.version)>($3,$4,-$5::integer))
  ORDER BY s.name,s.set_id,v.version DESC LIMIT $6;
  """;
  command.Parameters.AddWithValue(componentCode);
  command.Parameters.Add(new Npgsql.NpgsqlParameter {NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)setId??DBNull.Value});
  if(limit is <1 or >200)throw new ArgumentOutOfRangeException(nameof(limit));
  command.Parameters.AddWithValue(afterName);
  command.Parameters.Add(new Npgsql.NpgsqlParameter{NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)afterSetId??DBNull.Value});
  command.Parameters.AddWithValue(afterVersion);command.Parameters.AddWithValue(limit);
  await using var reader=await command.ExecuteReaderAsync(token);
  var rows=new List<ParameterSetVersion>();
  while(await reader.ReadAsync(token))
  {
   var value=JsonSerializer.Deserialize<ParameterSetVersion>(reader.GetString(0))??throw new InvalidDataException("Invalid parameter projection.");
   rows.Add(value with {Name=reader.GetString(1),Description=reader.GetString(2),CatalogRevision=reader.GetInt64(3)});
  }
  return rows.ToArray();
 }
}
