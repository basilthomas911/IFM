using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Framework.Serialization;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;
[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure","SelfContained")]
public sealed class PostgresRiskHistoryTests(PostgresTradePlanFixture fixture)
{
 [Fact] public async Task Risk_history_keysets_cover_tied_times_and_reject_cross_scope_cursors()
 {
  var portfolio=Random.Shared.Next(50_000_000,60_000_000);var date=new DateOnly(2026,10,13);var at=new DateTime(2026,10,13,12,0,0,DateTimeKind.Utc);var expected=Enumerable.Range(0,9).Select(_=>Guid.NewGuid()).ToArray();
  await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await db.ExecuteAsync(TradeSchemaSql.Create,[],ct);foreach(var invocation in expected){var row=new RiskHistoryRow(portfolio,1,date,at,Guid.NewGuid(),invocation,1,"Approved",2,"Test","Daily",123,"LongFuture");await db.ExecuteAsync("INSERT INTO trade.risk_management_history(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload) VALUES($1,1,$2,$3,$4,1,$5)",[portfolio,date,at,invocation,MessagePackBinarySerializer.Shared.Serialize(row)],ct);}return true;});
  byte[]? cursor=null;byte[]? first=null;var actual=new List<Guid>();
  do{var page=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.RiskReadHistoryAsync(db,portfolio,1,date,2,cursor,ct));Assert.InRange(page.Items.Length,1,2);actual.AddRange(page.Items.Select(x=>x.InvocationId));cursor=page.PagingState;first??=cursor;}while(cursor is not null);
  Assert.Equal(expected.OrderBy(x=>x.ToString("N"),StringComparer.Ordinal),actual);Assert.Equal(9,actual.Distinct().Count());Assert.NotNull(first);
  await Assert.ThrowsAsync<ArgumentException>(()=>fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.RiskReadHistoryAsync(db,portfolio,2,date,2,first,ct)));
  await Assert.ThrowsAsync<ArgumentException>(()=>fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.RiskReadHistoryAsync(db,portfolio,1,date,3,first,ct)));
 }
}
