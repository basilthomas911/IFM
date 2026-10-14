using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
public sealed class BrokerTradeLegSelectionTests
{
 [Theory][InlineData(TradeType.ShortIronCondor,false,true)][InlineData(TradeType.LongIronCondor,true,false)][InlineData(TradeType.PutCreditSpread,true,true)][InlineData(TradeType.PutDebitSpread,false,false)][InlineData(TradeType.CallCreditSpread,false,false)][InlineData(TradeType.CallDebitSpread,true,true)]public void Roles_follow_strike_order_instead_of_click_order(TradeType type,bool callLowLong,bool putLowLong){var condor=type is TradeType.ShortIronCondor or TradeType.LongIronCondor;var call=type.ToString().StartsWith("Call");var legs=new[]{new BrokerTradeLegData("C-HIGH","-SC",120,1,2,.1,true),new("C-LOW","+LC",110,1,2,.2,true),new("P-HIGH","+LP",90,1,2,-.2,false),new("P-LOW","-SP",80,1,2,-.1,false)}.Where(x=>condor||x.IsCall==call).ToArray();var result=BrokerTradeLegSelection.Normalize(type,legs);if(condor||call)Assert.Equal(callLowLong?"+LC":"-SC",result.Single(x=>x.ContractId=="C-LOW").Role);if(condor||!call)Assert.Equal(putLowLong?"+LP":"-SP",result.Single(x=>x.ContractId=="P-LOW").Role);}
}
