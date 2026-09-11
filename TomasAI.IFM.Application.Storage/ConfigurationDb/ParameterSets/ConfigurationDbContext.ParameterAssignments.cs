using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public sealed partial class ConfigurationDbContext
{
 public async Task ProjectParameterAssignmentAsync(ParameterAssignmentChangedEvent fact,CancellationToken token=default)
 {
  var value=JsonSerializer.Deserialize<ParameterAssignmentRevision>(fact.AssignmentJson)??throw new InvalidDataException("Invalid assignment.");
  if(value.AssignmentId!=fact.EntityId.AssignmentId||value.Revision!=fact.Revision)throw new InvalidDataException("Assignment identity mismatch.");
  await using var connection=await OpenCatalogAsync(token);await using var tx=await connection.BeginTransactionAsync(token);
  await Scalar(connection,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0));",token,"parameter-assignment:"+fact.EntityId.Format());
  var receipt=await Scalar(connection,tx,"SELECT request_sha256 FROM reference_configuration.parameter_assignment_revision WHERE operation_id=$1",token,fact.CommandId);
  if(receipt is string prior){if(prior!=fact.RequestHash)throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");return;}
  var revision=Convert.ToInt64(await Scalar(connection,tx,"SELECT revision FROM reference_configuration.parameter_assignment WHERE assignment_id=$1",token,value.AssignmentId)??0L);
  if(revision+1!=value.Revision)throw new InvalidOperationException("PARAM.PROJECTION_ORDER");
  await Execute(connection,tx,"INSERT INTO reference_configuration.parameter_assignment(assignment_id,revision,body) VALUES($1,$2,$3::jsonb) ON CONFLICT(assignment_id) DO UPDATE SET revision=EXCLUDED.revision,body=EXCLUDED.body",token,value.AssignmentId,value.Revision,fact.AssignmentJson);
  await Execute(connection,tx,"INSERT INTO reference_configuration.parameter_assignment_revision(assignment_id,revision,operation_id,request_sha256,body) VALUES($1,$2,$3,$4,$5::jsonb)",token,value.AssignmentId,value.Revision,fact.CommandId,fact.RequestHash,fact.AssignmentJson);
  if(!string.IsNullOrWhiteSpace(fact.AuditJson))
  {
   var audit=JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson)??throw new InvalidDataException("Invalid audit entry.");
   if(audit.OperationId!=fact.CommandId||audit.Revision!=fact.Revision)throw new InvalidDataException("Audit identity mismatch.");
   await Execute(connection,tx,"INSERT INTO reference_configuration.parameter_set_audit(operation_id,entity_id,revision,body) VALUES($1,$2,$3,$4::jsonb)",token,audit.OperationId,audit.EntityId,audit.Revision,fact.AuditJson);
  }
  await tx.CommitAsync(token);
 }
}
