using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
public class ContractEditorTests
{
 static ContractEditorIntent Request(string action,long generation=0,string? original=null)=>new(1,Guid.NewGuid(),"futures-contract",action,generation,original,new(){["symbol"]="ES"});
 [Fact] public async Task Mutation_requires_current_snapshot_identity_and_duplicate_executes_once()
 {
  int calls=0;var completion=new TaskCompletionSource<ContractEditorResult>();
  var gateway=new ContractEditorGateway(input=>{Interlocked.Increment(ref calls);return calls==1?Task.FromResult(new ContractEditorResult(input.View,[new("exact-original",input.Fields)],[])):completion.Task;});
  Assert.Equal(-5,gateway.Submit(Request("change",1,"missing")));Assert.Equal(0,gateway.Submit(Request("load")));await gateway.DrainAsync();
  var generation=gateway.Snapshot().Generation;
  Assert.Equal(-5,gateway.Submit(Request("change",generation-1,"exact-original")));
  Assert.Equal(-5,gateway.Submit(Request("remove",generation,"missing")));
  var input=Request("change",generation,"exact-original");Assert.Equal(0,gateway.Submit(input));Assert.Equal(0,gateway.Submit(input));
  Assert.Equal(-1,gateway.Submit(input with{Intent="remove"}));Assert.True(gateway.Snapshot().Busy);
  completion.SetResult(new(input.View,[new("exact-original",input.Fields)],[]));await gateway.DrainAsync();
  Assert.Equal(2,calls);Assert.Equal("Completed",gateway.Snapshot().Outcomes.Last().State);
 }
 [Fact] public void Provider_catalog_rejects_mutation_intents_before_execution()
 {
  var gateway=new ContractEditorGateway(_=>throw new InvalidOperationException("Must not execute"));
  foreach(var intent in new[]{"add","change","remove","import","range","review"})
   Assert.Equal(-1,gateway.Submit(Request(intent) with{View="provider-definitions"}));
 }
 [Fact] public async Task Unknown_completion_requires_a_reload_before_another_mutation()
 {
  var gateway=new ContractEditorGateway(input=>input.Intent=="load"?Task.FromResult(new ContractEditorResult(input.View,[new("exact-original",input.Fields)],[])):throw new EditorOutcomeUnknownException("Reload and verify"));
  Assert.Equal(0,gateway.Submit(Request("load")));await gateway.DrainAsync();Assert.Equal(0,gateway.Submit(Request("change",gateway.Snapshot().Generation,"exact-original")));await gateway.DrainAsync();
  Assert.Equal("Unknown",gateway.Snapshot().Outcomes.Last().State);Assert.Equal(-5,gateway.Submit(Request("change",gateway.Snapshot().Generation,"exact-original")));
  Assert.Equal(0,gateway.Submit(Request("load")));await gateway.DrainAsync();Assert.Equal("futures-contract",gateway.Snapshot().Result.View);
 }
 [Fact] public void Provider_edits_preserve_all_reference_evidence_and_only_change_allowed_fields()
 {
  var input=Request("change") with{Fields=new(){["description"]="New label",["onTheRun"]="true",["symbol"]="WRONG",["contractId"]="WRONG"}};
  var futures=new FuturesContractV3ReadModel{SchemaVersion=1,ContractId="ProviderId",Symbol="ES",Description="Old",Dataset="GLBX.MDP3",DefinitionDigest="digest",LastTradeDate=new(2026,12,18),Rollover=true};
  var changed=ContractDraftMapping.Futures(input,futures);
  Assert.Equal(futures with{Description="New label",OnTheRun=true},changed);
  var option=new FuturesOptionContractReadModel{SchemaVersion=1,ContractId="ProviderOption",Symbol="ES",Dataset="GLBX.MDP3",DefinitionDigest="option-digest",StrikePrice=6500,StrikePriceDecimal=6500};
  Assert.Equal(option with{Description="New label"},ContractDraftMapping.Option(input,option));
 }
 [Fact] public void Manual_option_change_cannot_change_identity_and_exact_strike_is_preserved()
 {
  var original=new FuturesOptionContractReadModel{ContractId="ES20261218C6500",Symbol="ES",ContractMonth=new(2026,12,18),StrikePrice=6500,StrikePriceDecimal=6500,OptionType="Call"};
  var input=Request("change") with{View="option-contract",Fields=new(){["symbol"]="ES",["contractMonth"]="2026-12-18",["strikePrice"]="6500",["optionType"]="Call",["description"]="Updated"}};
  var changed=ContractDraftMapping.Option(input,original);Assert.Equal(original.ContractId,changed.ContractId);Assert.Equal(6500m,changed.StrikePriceDecimal);
  input.Fields["strikePrice"]="6500.25";Assert.Throws<ArgumentException>(()=>ContractDraftMapping.Option(input,original));
  var added=ContractDraftMapping.Option(input,null);Assert.Equal("ES20261218C6500.25",added.ContractId);Assert.Equal(6500.25m,added.StrikePriceDecimal);
 }
 [Fact] public async Task Timed_out_editor_completion_is_unknown_instead_of_a_false_failure()
 {
  var gateway=new EditorActionGateway(_=>throw new EditorOutcomeUnknownException("Reload and verify"));
  Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"lookup","load",0,null)));await gateway.DrainAsync();Assert.Equal("Unknown",gateway.Snapshot().Outcomes.Single().State);
 }
}
