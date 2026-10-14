using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.TradeBroker.UnitTests;
public sealed class BrokerAccountPostgresProjectionTests
{
 [Fact][Trait("Category","Integration")]public async Task Positional_parameters_preserve_highest_revision_and_idempotent_replay(){
  var name="ifm_blotter_broker_"+Guid.NewGuid().ToString("N");using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>("Host=localhost;Port=5432;Database=postgres");await admin.OpenAsync();await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
  try{var connectionString="Host=localhost;Port=5432;Database="+name+";Pooling=false";using(var isolated=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString)){await isolated.OpenAsync();await using var schema=new NpgsqlCommand("CREATE SCHEMA trade; CREATE TABLE trade.broker_account_read_model(account_alias text,revision bigint,account_definition bytea,PRIMARY KEY(account_alias,revision));",isolated);await schema.ExecuteNonQueryAsync();}
   var settings=new DbConnectionSettings().Add("TradeDbConnection",connectionString,"System.Data.Postgres");var store=new BrokerAccountReadStore(settings,NullLogger<DbProvider>.Instance);var id=new BrokerAccountId("ISOLATED-TEST");var account=new BrokerAccountDefinition{Id=id,Revision=2,QualificationStatus=BrokerAccountQualificationStatus.Accepted,Gate=BrokerAccountOperationalGate.Open};await store.ProjectAsync(account);await store.ProjectAsync(account with{Revision=1,Gate=BrokerAccountOperationalGate.Closed});await store.ProjectAsync(account);Assert.Equal(account,await new BrokerAccountReadStore(settings,NullLogger<DbProvider>.Instance).GetAsync(id));Assert.Null(await store.GetAsync(new("MISSING")));
  }finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
 }
}
