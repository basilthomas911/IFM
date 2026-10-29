using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Framework.MarketData.DataBento;
var assembly=Assembly.Load("TomasAI.IFM.Framework.MarketData.DataBento");
var query=assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.DatabentoMarketDataQueries",true)!;
var parse=query.GetMethod("ParseContractId",BindingFlags.NonPublic|BindingFlags.Static)!;
var inputs=new List<string>{"ES20261218","ES20261218C6500","ES20261218P6500.5","M2K20261218C0.000000001","ES20261218C9223372036.854775807","ES20261218C9223372036.854775808","ES20260229","ES00001218","ES20261218C0","ES20261218C1.","ES20261218C.5","ES20261218C-1","ES20261218C1.0000000000","ES20261218C1.0000000001","ES20261218C1.00000000000000000000000000001","","es20261218"," ES20261218","ES20261218 ","ES12320261218","ES20261218C001.25000"};
var rng=new Random(5050);
for(int i=0;i<1000;i++){var price=(rng.NextInt64(1,long.MaxValue)/1000000000m).ToString(System.Globalization.CultureInfo.InvariantCulture);inputs.Add($"ES{2020+rng.Next(10):D4}{rng.Next(0,14):D2}{rng.Next(0,34):D2}{(rng.Next(2)==0?"C":"P")}{price}");}
var identities=new List<object>();
foreach(var id in inputs){try{var value=parse.Invoke(null,[id])!;object? P(string n)=>value.GetType().GetProperty(n)!.GetValue(value);identities.Add(new{id,valid=true,ticker=P("Ticker"),expiration=((DateOnly)P("Expiration")!).ToString("yyyy-MM-dd"),kind=Convert.ToInt32(P("Kind")),strike=P("StrikePrice")});}catch{identities.Add(new{id,valid=false});}}
var seed=JsonNode.Parse(File.ReadAllText(Path.Combine(args[0],"DefinitionOracle.json")))![0]!["json"]!.GetValue<string>();
var definitions=new List<string>();var parsed=new List<ContractDetail>();
for(int i=0;i<100;i++){
 var d=JsonNode.Parse(seed)!;d["hd"]!["instrument_id"]=i%13+1;d["raw_symbol"]="O"+(i%17);d["instrument_class"]=new[]{"C","P","F","X"}[rng.Next(4)];d["asset"]=i%3==0?"ES":"OTHER";d["underlying"]=new[]{"ESZ6","ESOTHER","VX",""}[rng.Next(4)];d["underlying_id"]=rng.Next(0,4)==0?42:99;d["strike_price"]=i%7==0?long.MaxValue:rng.Next(0,5)*1000000000L;d["expiration"]=i%4==0?"1798277400000000000":"1798191000000000000";
 var text=d.ToJsonString();definitions.Add(text);parsed.Add(ExactInstrumentDefinition.Parse("GLBX.MDP3",text).Summary);
}
var selected=ExactInstrumentDefinition.Parse("GLBX.MDP3",seed).Summary;
var create=assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.OptionChainDefinitionFilter",true)!.GetMethod("Create",BindingFlags.NonPublic|BindingFlags.Static)!;
var chains=new List<object>();
foreach(byte policy in new byte[]{1,2,3})foreach(byte rights in new byte[]{0,1,2,3})foreach(var useSelected in new[]{false,true})foreach(var root in new[]{"ES","VX"}) {
 var request=new OptionChainDefinitionRequest{Dataset="GLBX.MDP3",Underlying=root,MaturityDate=selected.MaturityDate!.Value,UniversePolicy=(OptionUniversePolicy)policy,Rights=(OptionRightSelection)rights};
 var result=(OptionChainDefinitions)create.Invoke(null,["GLBX.MDP3",request,useSelected?selected:null,parsed])!;
 chains.Add(new{policy,rights,useSelected,root,maturity=request.MaturityDate.ToString("yyyy-MM-dd"),ids=result.Contracts.Select(d=>d.Instrument.InstrumentId),symbols=result.Contracts.Select(d=>d.RawSymbol)});
}
File.WriteAllText(Path.Combine(args[0],"SelectionOracle.json"),JsonSerializer.Serialize(new{identities,definitions,selected=seed,chains}));Console.WriteLine($"C# selection: {identities.Count} canonical IDs; {chains.Count} chain selections");
