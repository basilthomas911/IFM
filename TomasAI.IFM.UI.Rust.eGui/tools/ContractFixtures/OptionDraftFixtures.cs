using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
internal static class OptionDraftFixtures {
 public static void Export(string output) {
  var cases=new List<object>();
  foreach(var type in new[]{TradeType.ShortIronCondor,TradeType.LongIronCondor,TradeType.PutCreditSpread,TradeType.PutDebitSpread,TradeType.CallCreditSpread,TradeType.CallDebitSpread}) {
   var all=new[]{new BrokerTradeLegData("C-HIGH","-SC",6100,1,2,.1,true),new BrokerTradeLegData("C-LOW","+LC",6050,2,3,.16,true),new BrokerTradeLegData("P-HIGH","+LP",5950,2,3,-.16,false),new BrokerTradeLegData("P-LOW","-SP",5900,1,2,-.1,false)};
   var selected=type is TradeType.ShortIronCondor or TradeType.LongIronCondor?all:all.Where(l=>l.IsCall==type.ToString().StartsWith("Call")).ToArray();
   var expected=BrokerTradeLegSelection.Normalize(type,selected);
   cases.Add(new{strategy=type.ToString(),input=selected.Select(l=>new{id=l.ContractId,role=l.Role}),expected=expected.Select(l=>new{id=l.ContractId,role=l.Role,sign=l.Sign})});
  }
  FixtureFiles.WriteAllText(Path.Combine(output,"option-draft-roles.json"),JsonSerializer.Serialize(cases));
 }
}
