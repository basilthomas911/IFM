using Npgsql;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public sealed partial class ConfigurationDbContext
{
 // Serializes lifecycle/assignment decisions across hosts while the event stream remains authoritative.
 public async Task<IAsyncDisposable> AcquireParameterWriteLeaseAsync(CancellationToken token=default)
 {
  var connection=await OpenCatalogAsync(token);NpgsqlTransaction? transaction=null;
  try
  {
   transaction=await connection.BeginTransactionAsync(token);
   await Execute(connection,transaction,"SET LOCAL lock_timeout = '5s'",token);
   await Execute(connection,transaction,"SELECT pg_advisory_xact_lock(hashtextextended('reference.parameter-sets.writer',0))",token);
   return new ParameterWriteLease(connection,transaction);
  }
  catch {if(transaction is not null)await transaction.DisposeAsync();await connection.DisposeAsync();throw;}
 }
 sealed class ParameterWriteLease(NpgsqlConnection connection,NpgsqlTransaction transaction):IAsyncDisposable
 {
  public async ValueTask DisposeAsync()
  {
   try {await transaction.DisposeAsync();}
   finally {await connection.DisposeAsync();}
  }
 }
}
