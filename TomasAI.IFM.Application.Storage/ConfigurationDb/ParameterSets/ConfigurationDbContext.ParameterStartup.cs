using Npgsql;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public sealed partial class ConfigurationDbContext
{
 public async Task ProjectParameterStartupAsync(ParameterStartupChangedEvent fact,CancellationToken token=default)
 {
  await using var connection=await OpenCatalogAsync(token);
  await using var transaction=await connection.BeginTransactionAsync(token);
  if(!string.IsNullOrEmpty(fact.ReportJson))
  {
   await Execute(connection,transaction,"INSERT INTO reference_configuration.parameter_startup_report(run_id,revision,body) VALUES($1,$2,$3::jsonb) ON CONFLICT(run_id,revision) DO NOTHING",token,fact.RunId,fact.Revision,fact.ReportJson);
   var same=await Scalar(connection,transaction,"SELECT body=$3::jsonb FROM reference_configuration.parameter_startup_report WHERE run_id=$1 AND revision=$2",token,fact.RunId,fact.Revision,fact.ReportJson);
   if(same is not true)throw new InvalidDataException("PARAM.STARTUP_REPORT_IMMUTABLE");
  }
  else if(!fact.Released)
  {
   await Execute(connection,transaction,"INSERT INTO reference_configuration.parameter_startup_run(run_id,body) VALUES($1,$2::jsonb) ON CONFLICT(run_id) DO NOTHING",token,fact.RunId,fact.RunJson);
   var same=await Scalar(connection,transaction,"SELECT body=$2::jsonb FROM reference_configuration.parameter_startup_run WHERE run_id=$1",token,fact.RunId,fact.RunJson);
   if(same is not true)throw new InvalidDataException("PARAM.STARTUP_IMMUTABLE");
  }
  else
  {
   var count=await Execute(connection,transaction,"UPDATE reference_configuration.parameter_startup_run SET active=false WHERE run_id=$1",token,fact.RunId);
   if(count!=1)throw new InvalidOperationException("PARAM.STARTUP_PROJECTION_PENDING");
  }
  await transaction.CommitAsync(token);
 }
}
