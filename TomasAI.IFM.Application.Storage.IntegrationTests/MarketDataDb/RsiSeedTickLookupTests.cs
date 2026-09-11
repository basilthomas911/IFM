using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Requires the dedicated parameter_sets_rsi_test keyspace on localhost; never uses an application keyspace.</summary>
public sealed class RsiSeedTickLookupTests
{
 [Fact]
 public async Task Indexed_lookup_returns_nearest_preceding_tick_and_never_a_future_tick()
 {
  var settings=new DbConnectionSettings().Add("MarketDataDbConnection","Contact Points=localhost;Port=9042;Default Keyspace=parameter_sets_rsi_test","System.Data.ScyllaDb");
  var logger=Substitute.For<ILogger<DbProvider>>();
  await new MarketDataSchemaDb(settings,logger).CreateAllAsync();
  var repositories=new Dictionary<Type,IObjectRepository>();
  var factory=new DbContextFactory(new DbContextResolver(type=>repositories[type]));
  var board=new BlackboardService(Substitute.For<IRedisCache>(),new SystemTextJsonSerializer());
  var db=new Storage.MarketDataDb.MarketDataDbContext(settings,factory,board,Substitute.For<ISequenceIdGenerator>(),logger);
  repositories.Add(typeof(IObjectRepository<Storage.MarketDataDb.MarketDataDbContext>),db);
  await db.BackfillQueryProjectionsV2Async(batchSize:64,staleOperationCutoffUtc:DateTime.UtcNow);
  var contract="RSI-SEED-"+Guid.NewGuid().ToString("N");var date=new DateOnly(2026,8,25);
  var rows=new[]{SampleData.FuturesTickData with {ContractId=contract,ValueDate=date,TickId=1,TickTime=new(12,0,0),Price=100m},
   SampleData.FuturesTickData with {ContractId=contract,ValueDate=date,TickId=2,TickTime=new(12,1,0),Price=101m}};
  await db.InsertFuturesTickDataAsync(rows);
  Assert.Equal(1,(await db.GetFuturesTickAtOrBeforeAsync(contract,date,new(12,0,30)))!.TickId);
  Assert.Equal(2,(await db.GetFuturesTickAtOrBeforeAsync(contract,date,new(12,1,0)))!.TickId);
  Assert.Null(await db.GetFuturesTickAtOrBeforeAsync(contract,date,new(11,59,59)));
  Assert.Null(await db.GetFuturesTickAtOrBeforeAsync(contract,date.AddDays(1),new(12,1,0)));
 }
}
