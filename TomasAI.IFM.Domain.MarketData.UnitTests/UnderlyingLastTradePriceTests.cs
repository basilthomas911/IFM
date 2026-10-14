using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
namespace TomasAI.IFM.Domain.MarketData.UnitTests;
public sealed class UnderlyingLastTradePriceTests
{
 [Fact]public void Last_is_the_exact_contracts_last_trade_not_a_quote_midpoint_or_pricing_fallback(){
  var at=DateTimeOffset.UtcNow.AddMinutes(-10);
  var snapshot=new FuturesMarketPriceSnapshot("ES",1,1,AssetTypeId.Futures,new(2026,10,14),
   new(5999m,1,6001m,1,1,1,1,at,at),new(5000m,1,1,at,at));
  var api=Api(snapshot);var actual=UnderlyingLastTradePrice.Read(api,"ES");
  Assert.Equal(5000m,actual.Price);Assert.Equal(at,actual.At);
 }
 [Theory][InlineData("OTHER",5000,NormalizedTradeAction.New)]
 [InlineData("ES",0,NormalizedTradeAction.New)]
 [InlineData("ES",5000,NormalizedTradeAction.Cancel)]
 [InlineData("ES",5000,NormalizedTradeAction.Clear)]
 public void Unusable_or_different_contract_trades_do_not_become_Last(string contract,int price,NormalizedTradeAction action){
  var now=DateTimeOffset.UtcNow;
  var snapshot=new FuturesMarketPriceSnapshot(contract,1,1,AssetTypeId.Futures,new(2026,10,14),null,new(price,1,1,now,now,action));
  Assert.Null(UnderlyingLastTradePrice.Read(Api(snapshot),"ES").Price);
 }
 [Fact]public void Quote_only_cache_and_missing_cache_do_not_masquerade_as_last_trades(){
  var now=DateTimeOffset.UtcNow;
  var snapshot=new FuturesMarketPriceSnapshot("ES",1,1,AssetTypeId.Futures,new(2026,10,14),new(5999m,1,6001m,1,1,1,1,now,now),null);
  Assert.Null(UnderlyingLastTradePrice.Read(Api(snapshot),"ES").Price);
  Assert.Null(UnderlyingLastTradePrice.Read(null,"ES").Price);
 }
 static IMarketDataApi Api(FuturesMarketPriceSnapshot snapshot){
  var api=Substitute.For<IMarketDataApi>();
  api.TryGetLastTickPrice(Arg.Any<string>(),out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(call=>{call[1]=snapshot;return true;});
  return api;
 }
}
