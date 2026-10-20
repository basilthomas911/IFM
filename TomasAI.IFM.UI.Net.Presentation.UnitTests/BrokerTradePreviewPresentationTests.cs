using System.Globalization;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Trade.Shared.Order;
public sealed class BrokerTradePreviewPresentationTests
{
 [Fact] public void Presentation_formats_backend_totals_without_recalculating_from_leg_quotes()
 {
   var legs = new[]{new BrokerTradeLegData("SC","-SC",110,2,2.4m,.16,true,.2,50)};
   var totals = new TradeOrderPreview { HasCompleteQuotes=true,HasBalancedQuantities=true,OrderPrice=-9.15m,
       NetBid=-10m,NetAsk=-8m,NetMid=-9m,NetDelta=123.45,NetVega=67.89,
       HasPayoffEstimate=true,MaximumProfit=1234m,MaximumLoss=4321m,EstimatedCredit=999m,MaximumReturn=.25m };
   var result=BrokerTradePreviewPresentation.FromBackend(legs,totals,null);
   Assert.Equal(-9.15m,result.OrderPrice);
   Assert.Equal(1234m,decimal.Parse(result.Fields["Max profit"],CultureInfo.CurrentCulture));
   Assert.Equal(4321m,decimal.Parse(result.Fields["Max loss"],CultureInfo.CurrentCulture));
   Assert.Equal("123.45",result.Fields["Net delta"].Replace(',', '.'));
   Assert.Equal(999m,decimal.Parse(result.Fields["Est. credit"],CultureInfo.CurrentCulture));
 }
 [Fact] public void Incomplete_backend_result_does_not_fabricate_totals()
 {
   var result=BrokerTradePreviewPresentation.FromBackend([],new TradeOrderPreview(),null);
   Assert.Null(result.OrderPrice);Assert.Equal("Select priced strategy legs",result.Fields["Validation"]);
   Assert.False(result.Fields.ContainsKey("Max loss"));
 }
 [Fact] public void Futures_format_server_reference_and_preserve_entered_order_price()
 {
   var legs=new[]{new BrokerTradeLegData("ES","+Future",null,null,null,null,false,IsFuture:true)};
   var result=BrokerTradePreviewPresentation.FromBackend(legs,new TradeOrderPreview{OrderPrice=7801,NetDelta=100,NetVega=0},null);
   Assert.Equal(7801m,result.OrderPrice);Assert.Equal("Reference price only - market quote required",result.Fields["Validation"]);
 }
 [Fact]public void Stored_legs_preserve_contract_direction_multiplier_and_do_not_invent_quotes(){var rows=BrokerTradePreviewPresentation.StoredLegs(new TomasAI.IFM.Domain.Trade.Shared.TradeOrderComponentDefinition{Legs=[new(){ContractId="C",AssetFamily=TomasAI.IFM.Domain.Trade.Shared.TradeAssetFamily.FuturesOption,SignedQuantity=-2,PutCall=1,Strike=110,CashMultiplier=50},new(){ContractId="P",AssetFamily=TomasAI.IFM.Domain.Trade.Shared.TradeAssetFamily.FuturesOption,SignedQuantity=2,PutCall=2,Strike=90,CashMultiplier=50}]});Assert.Equal("-SC",rows[0].Role);Assert.Equal("+LP",rows[1].Role);Assert.Equal(50m,rows[0].Multiplier);Assert.All(rows,x=>{Assert.Null(x.Bid);Assert.Null(x.Ask);Assert.Null(x.Delta);});}
}
