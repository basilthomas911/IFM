using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Order;
/// <summary>Internally consistent frozen prices for screen/protocol parity.
/// They are synthetic snapshots, not claimed historical exchange observations.</summary>
internal static class ComposerOptionFixtures {
 static readonly DateOnly date=new(2026,10,26);
 static readonly DateTimeOffset at=new(StrategyFixtures.At);
 public static EvaluatedOptionChainReadModel Chain(bool frozen=true){
  EvaluatedOptionContractReadModel Leg(string id,decimal strike,bool call,decimal bid,decimal ask,double delta)=>new(id,strike,call,bid,ask,1,2,null,null,.2,null,delta,null,.3,null,null,null,null,true,frozen,at,null,at);
  return new("ESZ6",date.AddDays(2),6000,5900,6100,frozen?"Frozen emulator preview fixture":"Live",at,[Leg("C-SHORT",6050,true,10,11,.16),Leg("C-LONG",6100,true,3,4,.10),Leg("P-SHORT",5950,false,9,10,-.16),Leg("P-LONG",5900,false,2,3,-.10)]){
   BollingerStandardDeviation20=25,PreviousSessionClose=6050.25m,LastUnderlyingPrice=6000,LastUnderlyingTradeAtUtc=at,AtmImpliedVolatility=.2m,IvRank=45,IvRankStatus="Ready",DaysToExpiry=2};
 }
 public static TradeOrderPreview Preview(long revision,decimal? entered=null)=>new(){InputRevision=revision,HasCompleteQuotes=true,HasBalancedQuantities=true,OrderPrice=entered??-14,NetBid=-16,NetAsk=-12,NetMid=-14,NetDelta=0,NetVega=0,CallWidth=50,PutWidth=50,CallSpreadValue=350,PutSpreadValue=350,EstimatedCredit=700,MaximumProfit=700,MaximumLoss=1800,MaximumReturn=700m/1800m,HasPayoffEstimate=true,ResolvedTickIncrement=.05m};
 public static ParameterStartupRun Startup {
  get {
   var id=Guid.Parse("321dd687-7a5a-4fa6-a404-01545cbab900");
   var parameters=ParameterSetValue.From(new IronCondorMarketSelectionParameterSet{ParameterSetId=id,Version=1,Symbols=[new(){Symbol="ES",ShortCallDelta=16,ShortPutDelta=16,CallSpreadWidth=50,PutSpreadWidth=50}]});
   var reference=new ParameterVersionRef(id,1,ParameterSchemaRegistry.IronCondorMarketSelectionComponent,ParameterValueCodec.Hash(parameters));
   var version=new ParameterSetVersion(reference,"Frozen ES defaults","Parity fixture",1,ParameterVersionStatus.Published,"",StrategyFixtures.At,"Fixture"){Parameters=parameters};
   var scope=new ParameterAssignmentScope("fixture","native","market-selection",reference.ComponentCode,"{}","fixture");
   var assignment=new ParameterAssignmentRevision(Guid.Parse("421dd687-7a5a-4fa6-a404-01545cbab900"),scope,1,reference,true,ParameterApplicationPolicy.NextStartup,StrategyFixtures.At,"Fixture");
   var run=Guid.Parse("521dd687-7a5a-4fa6-a404-01545cbab900");
   return new(run,[assignment],[version],new(run,"assignments","plan",[],[]),StrategyFixtures.At,"Fixture");
  }
 }
}
