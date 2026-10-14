using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
public sealed class OptionChainDisplaySnapshotTests
{
 [Fact]public void Partial_refresh_overwrites_arrived_values_and_retains_other_cells_and_contracts(){
  var first=Chain([Contract("short",100m) with{Bid=2m,Ask=3m,Delta=.16,Volume=100,GreeksValid=true},Contract("wing",150m)]);
  var next=Chain([Contract("short",100m) with{Bid=2.5m,Ask=null,Delta=null,Volume=0,GreeksValid=false,IsStale=true},Contract("new",200m)]);
  var merged=OptionChainDisplaySnapshot.Merge(first,next);var row=merged.Contracts.Single(x=>x.ContractId=="short");
  Assert.Equal(3,merged.Contracts.Length);Assert.Equal(2.5m,row.Bid);Assert.Equal(3m,row.Ask);Assert.Equal(.16,row.Delta);Assert.Equal(0,row.Volume);
  Assert.True(row.IsStale);Assert.False(row.GreeksValid); // Display retention must not make the latest input eligible.
  Assert.Null(next.Contracts[0].Ask);Assert.Null(next.Contracts[0].Delta); // The raw trading snapshot stays untouched.
 }
 [Fact]public void Expiry_underlying_and_frozen_live_transitions_reset_display_cache(){
  var first=Chain([Contract("old",100m)]);
  foreach(var next in new[]{Chain([Contract("new",100m)]) with{ExpiryDate=new(2026,12,18)},Chain([Contract("new",100m)]) with{UnderlyingContractId="OTHER"},Chain([Contract("new",100m)]) with{WindowMethod="Frozen emulator preview"}})
   Assert.Equal("new",Assert.Single(OptionChainDisplaySnapshot.Merge(first,next).Contracts).ContractId);
 }
 static EvaluatedOptionChainReadModel Chain(EvaluatedOptionContractReadModel[] rows)=>new("ES",new(2026,11,20),100m,50m,200m,"Live",DateTimeOffset.UtcNow,rows);
 static EvaluatedOptionContractReadModel Contract(string id,decimal strike)=>new(id,strike,true,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,false,false,null,null,null);
}
