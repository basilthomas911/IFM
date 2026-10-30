using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
/// <summary>Read-only trade workspace authority for isolated tests. No order,
/// financial command, live subscription or real database is constructed.</summary>
internal sealed partial class TradeWorkspaceResponder {
 string mode="Disabled";
 public string Mode{get=>mode;set{if(value is "ResetCommands" or "ResetEmptyCommands"){ResetCommands();if(value=="ResetEmptyCommands")tradeRows.Clear();mode="Commands";}else if(value=="CommitPending"){deferred?.Invoke();deferred=null;mode="Commands";}else mode=value;}}
 public bool Handles(string subject)=>Mode!="Disabled"&&(subject.StartsWith("Query.PortfolioQuery.GetPortfolios.",StringComparison.Ordinal)||subject.StartsWith("Query.PortfolioFundQuery.GetFunds.",StringComparison.Ordinal)||subject.StartsWith("Query.PortfolioFundQuery.GetFundOrdersPage.",StringComparison.Ordinal)||subject.StartsWith("Query.PortfolioFundQuery.GetFundOrderTradesPage.",StringComparison.Ordinal)||subject.StartsWith("Query.PortfolioFundQuery.GetFundOrderByOrderId.",StringComparison.Ordinal)||subject.StartsWith("Query.ReferenceQuery.GetNextSeedId.",StringComparison.Ordinal)||subject.StartsWith("Command.PortfolioFundCommand.",StringComparison.Ordinal));
 public static readonly FundOrderProjectionReadModel Order=new(){PortfolioId=1,FundId=2,OrderId=123,Status="Draft",CreatedOnUtc=StrategyFixtures.At,CreatedBy="Fixture",AggregateVersion=7,Origin=CompositionOrigin.ManualUi,OperatorReference="Native workspace fixture"};
 public static readonly FundOrderTradeProjectionReadModel Trade=new(){PortfolioId=1,FundId=2,OrderId=123,TradeId=456,TradeFamily="ShortIronCondor",TradeType="ShortIronCondor",TradeState="NewTrade",TradeAction="Sell",PrimaryTrade=true,BaseContractSymbol="ES",BaseContractId="ESZ6",UnderlyingRoot="ES",RequestedTradeDate=new(2026,10,26),CreatedOnUtc=StrategyFixtures.At,CreatedBy="Fixture",AggregateVersion=8};
 public byte[] Handle(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  void Check(object query){
   var type=query.GetType();
   if(type.GetProperty("Subject")!.GetValue(query)!.ToString()!=subject||type.GetProperty("RequestedOnUtc")!.GetValue(query) is not DateTime at||!DesktopFixtureMode.Clock(at,StrategyFixtures.At)||type.GetProperty("CorrelationId")!.GetValue(query) is not Guid correlation||correlation==Guid.Empty||type.GetProperty("Access")!.GetValue(query) is not PortfolioAccessContext access||access.Roles is not ["PortfolioReader"])throw new InvalidDataException("Incorrect native trade query metadata");
  }
  try{
   if(subject.StartsWith("Command.",StringComparison.Ordinal))return Command(subject,bytes);
   if(subject.StartsWith("Query.ReferenceQuery.GetNextSeedId.",StringComparison.Ordinal)){
    var q=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Reference.Shared.Queries.GetNextSeedIdQuery>(bytes,options);
    if(q.Subject.ToString()!=subject||q.SeedType!="TradeId"||q.EntityId.Format()!="TradeId")throw new InvalidDataException("Incorrect trade identity allocation");
    return Result(new ScalarReadModel<int>(++nextTrade));
   }
   if(subject.StartsWith("Query.PortfolioQuery.GetPortfolios.",StringComparison.Ordinal)){
    var q=MessagePackSerializer.Deserialize<GetPortfoliosQuery>(bytes,options);Check(q);
    if(q.PageSize!=200||q.State!=(int)PortfolioOperatingState.Active)throw new InvalidDataException("Incorrect active Portfolio query");
    return Result(new PortfolioPage<PortfolioReadModel>{Items=[PortfolioFixtures.Portfolio],PageSize=200});
   }
   if(subject.StartsWith("Query.PortfolioFundQuery.GetFunds.",StringComparison.Ordinal)){
    var q=MessagePackSerializer.Deserialize<GetFundsQuery>(bytes,options);Check(q);
    if(q.PortfolioId!=1||q.PageSize!=200||q.State is not null)throw new InvalidDataException("Incorrect Fund query");
    return Result(new PortfolioPage<FundMandateReadModel>{Items=Mode=="LatestInactive"?[PortfolioFixtures.Fund,PortfolioFixtures.Fund with{FundMandateVersion=2,OperatingState=FundOperatingState.Paused}]:[PortfolioFixtures.Fund],PageSize=200});
   }
   if(subject.StartsWith("Query.PortfolioFundQuery.GetFundOrdersPage.",StringComparison.Ordinal)){
    var q=MessagePackSerializer.Deserialize<GetFundOrdersPageQuery>(bytes,options);Check(q);
    if(q.PortfolioId!=1||q.FundId!=2||q.PageSize!=200||q.OrderMonth.Day!=1)throw new InvalidDataException("Incorrect monthly orders query");
    if(Mode=="RejectOrders")throw new InvalidDataException("Fixture order projection unavailable");
    var rows=ManualMode?orders.Values.Where(order=>new DateOnly(order.CreatedOnUtc.Year,order.CreatedOnUtc.Month,1)==q.OrderMonth).ToArray():q.OrderMonth==new DateOnly(2026,10,1)?new[]{Mode=="WrongScope"?Order with{FundId=999}:Order}:[];
    if(Mode=="Duplicate"&&rows.Length>0)rows=[Order,Order];
    return Result(new PortfolioPage<FundOrderProjectionReadModel>{Items=rows,PageSize=200,NextPageToken=Mode=="RepeatCursor"?"fixture-next":null});
   }
   if(subject.StartsWith("Query.PortfolioFundQuery.GetFundOrderByOrderId.",StringComparison.Ordinal)){
    var q=MessagePackSerializer.Deserialize<GetFundOrderByOrderIdQuery>(bytes,options);Check(q);
    if(Mode=="ReadUnavailable")return MessagePackSerializer.Serialize(new ServiceResult<FundOrderProjectionReadModel>{Success=false,ErrorCode=34014,ErrorMessage="Fixture projection unavailable"},options);
    return orders.TryGetValue(q.OrderId,out var found)?Result(found):MessagePackSerializer.Serialize(new ServiceResult<FundOrderProjectionReadModel>{Success=false,ErrorCode=34004,ErrorMessage="FundOrder was not found."},options);
   }
   var trades=MessagePackSerializer.Deserialize<GetFundOrderTradesPageQuery>(bytes,options);Check(trades);
   if((!ManualMode&&trades.OrderId!=123)||trades.PageSize!=200)throw new InvalidDataException("Incorrect order trades query");
   return Result(new PortfolioPage<FundOrderTradeProjectionReadModel>{Items=ManualMode?tradeRows.Values.Where(row=>row.OrderId==trades.OrderId).ToArray():Mode=="ComposeCloseFutures"?[Trade with{TradeId=455,TradeType="FuturesOutright",TradeAction="Buy",TradeState="Open",ExecutionOrderId=700,ExecutionTradeId=455},Trade with{TradeType="FuturesOutright",TradeAction="Sell",PrimaryTrade=false}]:[Mode=="WrongTradeScope"?Trade with{PortfolioId=999}:Mode=="ComposeFutures"?Trade with{TradeType="FuturesOutright",TradeAction="Buy"}:Mode=="ComposeOptions"?Trade with{RequestedMaturityDate=new(2026,10,30)}:Mode=="Monitor"?Trade with{TradeState="Open",ExecutionOrderId=701,ExecutionTradeId=456,RequestedMaturityDate=new(2026,10,30)}:Trade],PageSize=200});
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=34100,ErrorMessage=error.Message},options);}
 }
}
