using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
internal static class BrokerInitializationResponder {
 static string mode="Disabled";static int fallbacks;
 static readonly DateTime at=new(2026,10,26,12,30,0,DateTimeKind.Utc);static readonly DateOnly date=new(2026,10,26);
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.BrokerInit",StringComparison.Ordinal)||mode!="Disabled"&&new[]{"Query.GeneralLedgerQuery.GetAccountBalances.","Query.CapacityReservationQuery.GetCapacityUsage.","Query.PortfolioFundQuery.GetFundRiskEnvelope.","Query.PortfolioFundQuery.GetFundOrderByOrderId.","Query.FuturesContractQuery.GetFuturesContract.","Query.FuturesEodDataQuery.GetFuturesEodData.","Query.FuturesEodDataQuery.GetLastFuturesEodData.","Query.FuturesOptionTradeQuery.GetOptionTrade."}.Any(prefix=>subject.StartsWith(prefix,StringComparison.Ordinal));
 public static byte[]? Handle(string subject,byte[] payload){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  void Financial(FinancialReadScope scope,DateTime requested,Guid correlation){if(scope.PortfolioId!=1||scope.FundId!=2||scope.Access.Roles is not ["LedgerRead"]||!DesktopFixtureMode.Clock(requested,at)||correlation==Guid.Empty)throw new InvalidDataException("Incorrect scoped initialization financial request");}
  if(subject=="Query.NativeCompatibility.BrokerInitMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);fallbacks=0;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.BrokerInitCount.none")return Result(new ScalarReadModel<int>(fallbacks));
  if(mode=="Hang")return null;
  if(subject=="Query.GeneralLedgerQuery.GetAccountBalances.1"){
   var q=MessagePackSerializer.Deserialize<GetAccountBalancesQuery>(payload,options);Financial(q.Scope,q.RequestedAtUtc,q.CorrelationId);
   return Result(new FinancialRead<FinancialBalanceSnapshot>(FinancialReadStatus.Found,new(8,"Open",[],0,123456.78m,true),10,at));
  }
  if(subject=="Query.CapacityReservationQuery.GetCapacityUsage.1"){
   var q=MessagePackSerializer.Deserialize<GetCapacityUsageQuery>(payload,options);Financial(q.Scope,q.RequestedAtUtc,q.CorrelationId);
   return Result(new FinancialRead<FinancialCapacityUsage>(FinancialReadStatus.Found,new([]){ReservedFundLossCapital=321.25m},10,at));
  }
  if(subject=="Query.PortfolioFundQuery.GetFundRiskEnvelope.1.2"){
   var q=MessagePackSerializer.Deserialize<GetFundRiskEnvelopeQuery>(payload,options);
   // The current managed initialization passes default AsOfUtc, asking the
   // backend to choose its clock. Native requests carry the API instant.
   var asOf=DesktopFixtureMode.Clock(q.AsOfUtc,at)||DesktopFixtureMode.Enabled&&q.AsOfUtc==default;
   if(q.PortfolioId!=1||q.FundId!=2||!asOf||!DesktopFixtureMode.Clock(q.RequestedOnUtc,at)||q.CorrelationId==Guid.Empty)throw new InvalidDataException("Incorrect initialization risk clock/scope: "+MessagePackSerializer.ConvertToJson(payload,options));
   return Result(new FundRiskEnvelopeReadModel{PortfolioId=1,FundId=mode=="WrongScope"?3:2,AvailableCapital=10000,MaximumRiskPerTrade=1000});
  }
  if(subject=="Query.PortfolioFundQuery.GetFundOrderByOrderId.123")return Result(TradeWorkspaceResponder.Order);
  if(subject=="Query.FuturesContractQuery.GetFuturesContract.ESZ6"){
   var q=MessagePackSerializer.Deserialize<GetFuturesContractQuery>(payload,options);if(q.ContractId!="ESZ6")throw new InvalidDataException("Incorrect underlying");
   return Result(new FuturesContractV3ReadModel{ContractId="ESZ6",Symbol="ES",Multiplier="50"});
  }
  if(subject is "Query.FuturesEodDataQuery.GetFuturesEodData.ESZ6.2026-10-26" or "Query.FuturesEodDataQuery.GetLastFuturesEodData.ESZ6.2026-10-26"){
   var last=subject.Contains(".GetLastFuturesEodData.",StringComparison.Ordinal);
   var q=last?null:MessagePackSerializer.Deserialize<GetFuturesEodDataQuery>(payload,options);
   var l=last?MessagePackSerializer.Deserialize<GetLastFuturesEodDataQuery>(payload,options):null;
   if((q?.ValueDate??l!.ValueDate)!=date||(q?.ContractId??l!.ContractId)!="ESZ6")throw new InvalidDataException("EOD did not use API ValueDate");
   if(last)fallbacks++;
   return Result(new FuturesEodDataV2ReadModel{ContractId="ESZ6",ValueDate=last?date.AddDays(-1):date,ClosePrice=6050.25m,DailyStdDevAmount=mode=="EodIncomplete"&&!last?0:25});
  }
  if(subject=="Query.FuturesOptionTradeQuery.GetOptionTrade.123.456"){
   var q=MessagePackSerializer.Deserialize<GetOptionTradeQuery>(payload,options);if(q.OrderId!=123||q.TradeId!=456)throw new InvalidDataException("Wrong stored trade identity");
   if(mode=="TradeNotFound")return MessagePackSerializer.Serialize(new ServiceResult<OptionTradeReadModel>{Success=false,ErrorCode=404,ErrorMessage="New trade has no stored execution"},options);
   return Result(new OptionTradeReadModel{OrderId=123,TradeId=456,UnderlyingContractId="ESZ6",CreatedOn=at,UpdatedOn=at});
  }
  throw new InvalidDataException("Unsupported broker initialization route "+subject);
 }
}
