using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class StrategyCatalogBridgeTests
{
 static ContractEditorIntent Intent(string action,string? id=null,Dictionary<string,string>? fields=null)=>new(1,Guid.NewGuid(),"strategy-catalog",action,0,id,fields??[]);
 sealed class Fixture
 {
  public IReferenceQueryApi Queries=Substitute.For<IReferenceQueryApi>();public IReferenceCommandApi Commands=Substitute.For<IReferenceCommandApi>();public Dictionary<CatalogKey,StoredStrategyCatalogDefinition> Rows=[];public List<CatalogCommandRequest> Requests=[];public bool Fail;
  public Fixture(){foreach(var d in StrategyCatalogDefaults.Create().Where(x=>x.Key.Kind==StrategyCatalogKind.Family))Rows[d.Key]=new(d,"hash",CatalogLifecycleStatus.Draft,DateTime.UtcNow,"fixture",null,null,null,null);
   Queries.QueryStrategyCatalogAsync(Arg.Any<CatalogQueryRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{var q=call.Arg<CatalogQueryRequest>();return new ServiceOk<string>(q.Operation==CatalogQueryOperation.Exact?StrategyCatalogJson.Write(Rows[q.Key!]):StrategyCatalogJson.Write(Rows.Values.Where(x=>x.Definition.Key.Kind==q.Kind).GroupBy(x=>x.Definition.Key.Id).Select(x=>x.MaxBy(y=>y.Definition.Key.Version)!).Select(x=>new StrategyCatalogSummary(x.Definition.Key,x.Definition.Code,x.Definition.Name,x.Status,x.ContentHash)).ToArray()));});
   Queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([]));
   Commands.ExecuteStrategyCatalogAsync(Arg.Any<CatalogCommandRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{var r=call.Arg<CatalogCommandRequest>();Requests.Add(r);if(Fail)return new ServiceFailed<Guid>(503,"offline");if(r.Operation==CatalogCommandOperation.SaveDraft)Rows[r.Definition!.Key]=new(r.Definition,"hash",CatalogLifecycleStatus.Draft,DateTime.UtcNow,"fixture",null,null,null,null);else Rows[r.Key!]=Rows[r.Key!] with{Status=r.Operation==CatalogCommandOperation.Publish?CatalogLifecycleStatus.Published:CatalogLifecycleStatus.Retired};return (ServiceResult<Guid>)new ServiceOk<Guid>(r.OperationId);});
  }
 }
 [Fact]public async Task Versioned_edit_preserves_failed_working_copy_retries_operation_and_verifies_lifecycle()
 {
  var f=new Fixture();await using var session=new StrategyCatalogSession(f.Queries,f.Commands);var list=await session.ExecuteAsync(Intent("load"));Assert.Equal(3,list.Rows.Length);await session.ExecuteAsync(Intent("catalog-select",list.Rows[0].Id));var draft=await session.ExecuteAsync(Intent("catalog-edit"));var d=StrategyCatalogJson.Read<StrategyCatalogDefinition>(draft.Context!["definition"]) with{Name="Changed family"};Assert.Equal(2,d.Key.Version);f.Fail=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("catalog-save",fields:new(){["definition"]=StrategyCatalogJson.Write(d)})));var resumed=await session.ExecuteAsync(Intent("catalog-resume"));Assert.Equal("True",resumed.Context!["editing"]);Assert.Contains("Changed family",resumed.Context["definition"]);f.Fail=false;var saved=await session.ExecuteAsync(Intent("catalog-save",fields:new(){["definition"]=StrategyCatalogJson.Write(d)}));Assert.Equal(f.Requests[0].OperationId,f.Requests[1].OperationId);Assert.Equal("False",saved.Context!["editing"]);await session.ExecuteAsync(Intent("catalog-publish"));Assert.Equal("hash",f.Requests.Last().ExpectedHash);Assert.Equal(CatalogLifecycleStatus.Published,f.Rows[d.Key].Status);await session.ExecuteAsync(Intent("catalog-retire"));Assert.Equal(CatalogLifecycleStatus.Retired,f.Rows[d.Key].Status);
 }
 [Fact]public async Task Forged_working_identity_and_navigation_during_edit_are_rejected_without_writes()
 {
  var f=new Fixture();await using var session=new StrategyCatalogSession(f.Queries,f.Commands);var list=await session.ExecuteAsync(Intent("load"));await session.ExecuteAsync(Intent("catalog-select",list.Rows[0].Id));var draft=await session.ExecuteAsync(Intent("catalog-edit"));var d=StrategyCatalogJson.Read<StrategyCatalogDefinition>(draft.Context!["definition"]);await Assert.ThrowsAsync<ArgumentException>(()=>session.ExecuteAsync(Intent("catalog-save",fields:new(){["definition"]=StrategyCatalogJson.Write(d with{Key=d.Key with{Id=Guid.NewGuid()}})})));await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("load")));Assert.Empty(f.Requests);await session.ExecuteAsync(Intent("catalog-discard"));Assert.Equal("False",(await session.ExecuteAsync(Intent("catalog-resume"))).Context!["editing"]);
 }
 [Fact]public async Task Gateway_rejects_stale_versions_wrong_selection_and_cross_view_catalog_commands()
 {
  var gate=new ContractEditorGateway(_=>Task.FromResult(new ContractEditorResult("strategy-catalog",[new("fixture",[])],[],Context:new(){["selectedId"]="fixture"})));Assert.Equal(0,gate.Submit(Intent("load")));await gate.DrainAsync();var intent=Intent("catalog-edit","fixture")with{ExpectedGeneration=gate.Snapshot().Generation};Assert.Equal(-5,gate.Submit(intent with{ExpectedGeneration=intent.ExpectedGeneration-1}));Assert.Equal(-5,gate.Submit(intent with{OriginalId="other"}));Assert.Equal(-1,gate.Submit(intent with{View="operations-health"}));Assert.Equal(0,gate.Submit(intent));await gate.DrainAsync();
 }
}
