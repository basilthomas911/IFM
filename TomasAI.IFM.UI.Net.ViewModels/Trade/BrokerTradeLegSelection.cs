using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.UI.Net.ViewModels.Trade;
/// <summary>Shared strike-order roles for manually selected WinForms and Rust strategy legs.</summary>
public static class BrokerTradeLegSelection
{
 public static bool LowStrikeIsLong(TradeType type,bool call)=>type switch{TradeType.ShortIronCondor=>!call,TradeType.LongIronCondor=>call,TradeType.PutCreditSpread=>true,TradeType.CallDebitSpread=>true,_=>false};
 public static BrokerTradeLegData[] Normalize(TradeType type,IReadOnlyList<BrokerTradeLegData> legs){var required=type is TradeType.ShortIronCondor or TradeType.LongIronCondor?4:2;if(legs.Count!=required)return legs.ToArray();var result=legs.ToDictionary(x=>x.ContractId);foreach(var call in new[]{true,false}){var side=legs.Where(x=>x.IsCall==call).OrderBy(x=>x.Strike).ToArray();if(side.Length!=2)continue;for(var index=0;index<2;index++){var isLong=index==0?LowStrikeIsLong(type,call):!LowStrikeIsLong(type,call);var leg=side[index];result[leg.ContractId]=leg with{Role=(isLong?"+L":"-S")+(call?"C":"P")};}}return legs.Select(x=>result[x.ContractId]).ToArray();}
}
