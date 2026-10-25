using System.Text.Json;
using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Frozen selection oracle calls the unchanged helper. Decimal strings
/// cover exact sub-tick distances and full 96-bit boundaries, without quotes,
/// subscriptions, actors or order submission.</summary>
internal static class OptionSelectionFixtures {
 public static void Export(string output) {
  EvaluatedOptionContractReadModel Contract(string id, decimal strike, bool call, double? delta, bool stale=false, bool valid=true, bool selection=false)
   => new(id,strike,call,null,null,null,null,null,null,null,null,delta,null,null,null,null,null,null,valid,stale,null,null,null,SelectionValid:selection);
  var cases=new List<object>();
  void Add(string name, EvaluatedOptionContractReadModel[] contracts, bool call, decimal delta, decimal? underlying, decimal strike, decimal width, bool up, bool frozen=false) {
   cases.Add(new{name,contracts=contracts.Select(c=>new{c.ContractId,Strike=c.Strike.ToString(CultureInfo.InvariantCulture),c.IsCall,c.Delta,c.IsStale,c.GreeksValid,c.SelectionValid}),call,maximumDelta=(double)delta,underlying=underlying?.ToString(CultureInfo.InvariantCulture),strike=strike.ToString(CultureInfo.InvariantCulture),width=width.ToString(CultureInfo.InvariantCulture),up,frozen,
    candidates=OptionSpreadLegSelection.ShortCandidates(contracts,call,delta,underlying,frozen).Select(c=>c.ContractId),wing=OptionSpreadLegSelection.Wing(contracts.Select(c=>(c.ContractId,c.Strike)),strike,width,up)});
  }
  var normal=new[]{Contract("C2",6050,true,.16),Contract("C1",6050,true,.16),Contract("C3",6100,true,.10),Contract("ITM",5900,true,.6),Contract("ATM",6000,true,.5),Contract("ZERO",6001,true,0),Contract("STALE",6040,true,.16,true),Contract("INVALID",6040,true,.16,valid:false),Contract("SELECT",6060,true,.15,valid:false,selection:true),Contract("P1",5950,false,-.16),Contract("P2",5900,false,-.1),Contract("WRONG",6030,true,-.12),Contract("MISSING",6080,true,null)};
  Add("call",normal,true,.16m,6000,6050,50,true);
  Add("put",normal,false,.16m,6000,5950,50,false);
  Add("frozen",normal,true,.16m,6000,6050,50,true,true);
  Add("missing-wing",normal,true,.16m,null,6100,50,true);
  Add("zero-width",normal,true,.16m,null,6050,0,true);
  Add("negative-width",normal,true,.16m,null,6050,-1,true);
  var precise=new[]{Contract("A",1.0000000000000000000000000001m,true,.1),Contract("B",1.0000000000000000000000000002m,true,.1),Contract("C",1.0000000000000000000000000003m,true,.1)};
  Add("28-digit-scale",precise,true,.1m,1.0000000000000000000000000003m,1.0000000000000000000000000001m,.0000000000000000000000000002m,true);
  var boundary=new[]{Contract("MAX",decimal.MaxValue,true,.1),Contract("MIN",decimal.MinValue,true,.1),Contract("ZERO",0,true,.1)};
  // Avoid overflowing the original decimal subtraction; comparisons near each
  // edge still exercise values that cannot be represented exactly by f64.
  Add("96-bit-maximum",[Contract("A",decimal.MaxValue-1,true,.1),Contract("B",decimal.MaxValue,true,.1)],true,.1m,decimal.MaxValue,decimal.MaxValue-1,1,true);
  Add("negative-strike",[Contract("A",-50,false,-.1),Contract("B",-100,false,-.1),Contract("C",0,false,-.1)],false,.1m,-75,-50,50,false);
  Add("ordinal-tie",[Contract("\U00010000",1,true,.1),Contract("\uE000",1,true,.1)],true,.1m,null,0,1,true);
  FixtureFiles.WriteAllText(Path.Combine(output,"option-selection.json"),JsonSerializer.Serialize(cases));
  var previous=new EvaluatedOptionChainReadModel("ESZ6",new(2026,10,28),6000,null,null,"Live",DateTimeOffset.Parse("2026-10-26T12:30:00Z"),normal);
  var incoming=previous with {Contracts=[normal[0] with{Bid=2,Ask=null,IsStale=false,SelectionValid=true},Contract("NEW",6200,true,.05)]};
  var serializer=TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.ContentOptions;
  void Write(string name,EvaluatedOptionChainReadModel value)=>FixtureFiles.WriteAllBytes(Path.Combine(output,"option-display-"+name+".msgpack"),MessagePack.MessagePackSerializer.Serialize(value,serializer));
  Write("previous",previous);Write("incoming",incoming);
  Write("merged",TomasAI.IFM.UI.Net.ViewModels.Trade.OptionChainDisplaySnapshot.Merge(previous,incoming));
  var frozenIncoming=incoming with{WindowMethod="Frozen emulator preview fixture"};
  Write("frozen",frozenIncoming);Write("frozen-merged",TomasAI.IFM.UI.Net.ViewModels.Trade.OptionChainDisplaySnapshot.Merge(previous,frozenIncoming));
 }
}
