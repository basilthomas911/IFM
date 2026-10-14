using System.Text.Json;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class ParameterSetBridgeTests
{
 static (IParameterSetsApi Api,ParameterSetVersion Version) Fixture(){var api=Substitute.For<IParameterSetsApi>();var id=Guid.NewGuid();var payload=JsonSerializer.Serialize(new FuturesItiSignalParameterSet{ParameterSetId=id,Version=1});var version=new ParameterSetVersion(new(id,1,ParameterSchemaRegistry.FuturesItiSignalComponent,new string('a',64)),"Fixture ITI","",1,ParameterVersionStatus.Draft,payload,DateTime.UtcNow,"fixture",CatalogRevision:3);api.ComponentsAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<ParameterComponentSummary[]>([new("market-data","Market Data",version.Reference.ComponentCode,"Futures ITI",[1],true)]));api.VersionsAsync(Arg.Any<Guid?>(),Arg.Any<CancellationToken>(),Arg.Any<string>()).Returns(new ServiceOk<ParameterSetVersion[]>([version]));api.PreviewAsync(Arg.Any<Guid>(),Arg.Any<CancellationToken>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<bool>(),Arg.Any<string>()).Returns(new ServiceOk<string>(payload));return(api,version);}
 static ContractEditorIntent Intent(string action,string? selected=null,Dictionary<string,string>? fields=null)=>new(1,Guid.NewGuid(),"parameter-sets",action,0,selected,fields??[]);
 [Fact]public async Task Working_copy_uses_lossless_fields_and_save_verifies_the_exact_committed_receipt()
 {
  var(api,version)=Fixture();SaveParameterDraftCommand? saved=null;api.SaveAsync(Arg.Any<SaveParameterDraftCommand>(),Arg.Any<CancellationToken>()).Returns(call=>{saved=call.Arg<SaveParameterDraftCommand>();return new ServiceOk<GuidResult>(new(saved.CommandId));});api.StateAsync(version.Reference.SetId,Arg.Any<CancellationToken>()).Returns(_=>new ServiceOk<ParameterSetSnapshot>(new(version.Reference.SetId,4,[version with{Reference=version.Reference with{Version=2},PayloadJson=saved!.PayloadJson,CatalogRevision=4}],[new(saved!.CommandId,"fixture",4,version.Reference with{Version=2})])));
  await using var session=new ParameterSetSession(api);var loaded=await session.ExecuteAsync(Intent("load"));Assert.Equal(version.PayloadJson,loaded.Context!["payload"]);await session.ExecuteAsync(Intent("edit-draft"));var fields=ParameterFieldEditorModel.Read(version.PayloadJson).Where(x=>x.Path=="/DefaultTradingDays/Weekly").Select(x=>x with{Value="11"}).ToArray();Assert.Single(fields);var committed=await session.ExecuteAsync(Intent("save-draft",fields:new(){["changes"]=JsonSerializer.Serialize(fields),["name"]="Fixture ITI"}));Assert.NotNull(saved);Assert.Equal(3,saved.ExpectedRevision);Assert.Equal(11,JsonSerializer.Deserialize<FuturesItiSignalParameterSet>(saved.PayloadJson)!.DefaultTradingDays.Weekly);Assert.Equal("False",committed.Context!["editing"]);Assert.Equal("4",committed.Context["revision"]);Assert.Equal(version.Reference.SetId+":2",committed.Context["selectedId"]);
 }
 [Fact]public async Task Unknown_fields_preserve_the_exact_payload_and_prevent_editing_without_a_preview_call()
 {
  var(api,version)=Fixture();var node=System.Text.Json.Nodes.JsonNode.Parse(version.PayloadJson)!;node["FutureField"]=123;version=version with{PayloadJson=node.ToJsonString()};api.VersionsAsync(Arg.Any<Guid?>(),Arg.Any<CancellationToken>(),Arg.Any<string>()).Returns(new ServiceOk<ParameterSetVersion[]>([version]));await using var session=new ParameterSetSession(api);var loaded=await session.ExecuteAsync(Intent("load"));Assert.Equal("False",loaded.Context!["canEdit"]);Assert.Equal(version.PayloadJson,loaded.Context["payload"]);await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("edit-draft")));await api.DidNotReceive().PreviewAsync(Arg.Any<Guid>(),Arg.Any<CancellationToken>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<bool>(),Arg.Any<string>());
 }
 [Fact]public async Task Gateway_fences_selection_and_mutation_and_accepts_bounded_working_copy_fields_only_for_parameters()
 {
  var gate=new ContractEditorGateway(_=>Task.FromResult(new ContractEditorResult("parameter-sets",[new("fixture",[])],[],Context:new(){["selectedId"]="fixture"})));Assert.Equal(0,gate.Submit(Intent("load")));await gate.DrainAsync();var current=gate.Snapshot();var save=Intent("save-draft","fixture",new(){["changes"]=new string(' ',5000)})with{ExpectedGeneration=current.Generation};Assert.Equal(-5,gate.Submit(save with{ExpectedGeneration=current.Generation-1}));Assert.Equal(-5,gate.Submit(save with{OriginalId="other"}));Assert.Equal(-1,gate.Submit(save with{View="operations-health"}));Assert.Equal(0,gate.Submit(save));Assert.Equal(0,gate.Submit(save));await gate.DrainAsync();Assert.Equal(-1,gate.Submit(save with{RequestId=Guid.NewGuid(),Fields=new(){["changes"]=new string(' ',131073)}}));
 }
}
