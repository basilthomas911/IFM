using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
/// <summary>Only the isolated test broker installs this in-memory responder.</summary>
internal static class OptionChainResponder {
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.OptionChain",StringComparison.Ordinal)||subject.StartsWith("Query.MarketDataQuery.GetEvaluatedOptionChain.",StringComparison.Ordinal)||subject.StartsWith("Query.MarketDataQuery.GetDatabentoOptionChainRange.",StringComparison.Ordinal)||subject=="Query.TradeOrderQuery.GetTradeOrderPreview.Preview"||mode=="ComposeOptions"&&subject=="Query.ParameterSetQuery.GetParameterStartupRuns.none";
 static string mode="Normal";
 static readonly HashSet<string> leases=[];
 static readonly DateOnly date=new(2026,10,26);
 static readonly DateTimeOffset at=new(2026,10,26,12,30,0,TimeSpan.Zero);
 public static byte[]? Handle(string subject,byte[] payload) {
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  byte[] Error()=>MessagePackSerializer.Serialize(new ServiceResult<EvaluatedOptionChainReadModel>{Success=false,ErrorCode=1065,ErrorMessage="Fixture unavailable"},options);
  if(subject=="Query.NativeCompatibility.OptionChainMode.none") {mode=MessagePackSerializer.Deserialize<string>(payload,options);return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.OptionChainCount.none")return Result(new ScalarReadModel<int>(leases.Count));
  if(subject=="Query.ParameterSetQuery.GetParameterStartupRuns.none"){
   var q=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.GetParameterStartupRunsQuery>(payload,options);
   if(q.Subject.ToString()!=subject||q.Limit!=20||q.AfterRunId is not null||q.AfterCreatedAtUtc is not null)throw new InvalidDataException("Incorrect applied-default query");
   return Result(new[]{ComposerOptionFixtures.Startup});
  }
  if(subject=="Query.TradeOrderQuery.GetTradeOrderPreview.Preview") {
   var q=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Order.GetTradeOrderPreviewQuery>(payload,options);
   var p=q.TradeOrderPreviewRequest;
   if(mode is "ComposeFutures" or "ComposeCloseFutures"){
    if(p.InputRevision<=0||p.StrategyQuantity!=1||p.SelectedLegs is not [{ContractId:"ESZ6",IsFuture:true,Side:var side,Quantity:1,CashMultiplier:50}]||side!=(mode=="ComposeCloseFutures"?-1:1)||p.UnderlyingReferencePrice!=6050.25m)throw new InvalidDataException("Incorrect native futures draft");
    return Result(new TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreview{InputRevision=p.InputRevision,OrderPrice=p.EnteredOrderPrice??6050.25m,NetMid=6050.25m,NetDelta=50,NetVega=0,ResolvedTickIncrement=.25m});
   }
   if(mode=="ComposeOptions"){
    if(p.SelectedLegs.Length==0)return Result(new TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreview{InputRevision=p.InputRevision,ResolvedTickIncrement=.05m});
    var expected=ComposerOptionFixtures.Chain().Contracts;
    if(p.InputRevision<=0||p.StrategyQuantity!=1||p.SelectedLegs.Length!=4||p.UnderlyingReferencePrice!=6050.25m)throw new InvalidDataException("Incorrect option preview draft");
    for(var i=0;i<4;i++){var l=p.SelectedLegs[i];var e=expected[i];if(l.ContractId!=e.ContractId||l.Quantity!=1||l.Side!=(e.ContractId.EndsWith("SHORT",StringComparison.Ordinal)?-1:1)||l.Strike!=e.Strike||l.IsCall!=e.IsCall||l.IsFuture||l.Bid!=e.Bid||l.Ask!=e.Ask||l.Delta!=e.Delta||l.Vega!=e.Vega||l.CashMultiplier!=50)throw new InvalidDataException("Option preview changed canonical leg inputs");}
    return Result(ComposerOptionFixtures.Preview(p.InputRevision,p.EnteredOrderPrice));
   }
   if(q.Subject.ToString()!=subject||q.EntityId.Format()!="none"||p.InputRevision<=0||p.StrategyQuantity!=3||p.TickIncrement!=1||p.EnteredOrderPrice!=-.1234567890123456789012345678m||p.UnderlyingReferencePrice!=6050.25m||p.SelectedLegs.Length!=1)throw new InvalidDataException("Preview lost draft identity or exact decimal inputs");
   var leg=p.SelectedLegs[0];
   if(leg.ContractId!="ES-C"||leg.Side!=-1||leg.Quantity!=3||leg.Strike!=6050.0000000000000000000000001m||!leg.IsCall||leg.IsFuture||leg.Bid!=1.25m||leg.Ask!=1.5m||leg.Delta!=.16||leg.Vega!=.3||leg.CashMultiplier!=50)throw new InvalidDataException("Preview leg differs from canonical selection");
   if(mode=="Unavailable")return Error();
   return Result(new TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreview{InputRevision=p.InputRevision+(mode=="WrongRevision"?1:0),OrderPrice=p.EnteredOrderPrice,MaximumLoss=987.6543210123456789012345678m,MaximumProfit=null,NetDelta=.125,ResolvedTickIncrement=.05m});
  }
  if(subject=="Query.MarketDataQuery.GetDatabentoOptionChainRange.none") {
   var q=MessagePackSerializer.Deserialize<GetDatabentoOptionChainRangeQuery>(payload,options);
   if(q.Subject.ToString()!=subject||q.EntityId.Format()!="none"||q.UnderlyingSymbol!="ES"||q.FromMaturityDate!=date||q.ThroughMaturityDate!=date.AddDays(4))throw new InvalidDataException("Option expiry request did not use canonical API dates/scope");
   return Result(new[]{new OptionContractExpiryReadModel{Symbol=mode=="WrongExpirySymbol"?"VX":"ES",ContractId="ESZ6",ExpiryDate=date.AddDays(2),ProviderRoot="EW",RefreshedAtUtc=at.UtcDateTime},new OptionContractExpiryReadModel{Symbol="ES",ContractId="ESZ6",ExpiryDate=date.AddDays(-1),ProviderRoot="OLD",RefreshedAtUtc=at.UtcDateTime}});
  }
  if(subject!="Query.MarketDataQuery.GetEvaluatedOptionChain.none")throw new InvalidDataException("Invalid option-chain route");
  var query=MessagePackSerializer.Deserialize<GetEvaluatedOptionChainQuery>(payload,options);
  if(query.Subject.ToString()!=subject||query.EntityId.Format()!="none"||query.UnderlyingContractId!="ESZ6"||query.UnderlyingSymbol!="ES"||query.ExpiryDate<date||!Guid.TryParseExact(query.SubscriptionOwnerId,"N",out _)||query.StandardDeviationMultiplier!=2.5)throw new InvalidDataException("Invalid evaluated-chain scope/defaults/owner");
  if(query.ReleaseOnly) {
   if(query.RequiredContractIds.Length!=0||query.ProviderRoots.Length!=0||query.SpreadWingWidth!=0||query.AllowFrozenEmulatorPreview||query.FrozenEmulatorPreviewOnly)throw new InvalidDataException("Release request retained acquisition fields");
   if(mode=="ReleaseUnavailable")return Error();
   leases.Remove(query.SubscriptionOwnerId);return Result<EvaluatedOptionChainReadModel?>(null);
  }
  if(mode=="ComposeOptions"){
   if(query.SpreadWingWidth!=50||query.ProviderRoots is not ["EW"]||query.AllowFrozenEmulatorPreview!=query.FrozenEmulatorPreviewOnly)throw new InvalidDataException("Incorrect composed chain intent");
   leases.Add(query.SubscriptionOwnerId);return Result(ComposerOptionFixtures.Chain(query.FrozenEmulatorPreviewOnly));
  }
  if(query.ProviderRoots.Length!=1||query.ProviderRoots[0]!="EW"||query.SpreadWingWidth!=50.000000000000000000000000001m||query.AllowFrozenEmulatorPreview!=query.FrozenEmulatorPreviewOnly)throw new InvalidDataException("Option chain lost exact width or frozen-preview intent");
  leases.Add(query.SubscriptionOwnerId);
  if(mode=="Unavailable")return Error();
  if(mode=="LostReply")return null;
  var row=new EvaluatedOptionContractReadModel("ES-C",6050,true,1.25m,1.5m,1,2,null,null,.2,1.4,.16,.01,.3,-.1,0,null,null,true,false,at,null,at);
  return Result(new EvaluatedOptionChainReadModel(mode=="WrongContract"?"VXQ6":"ESZ6",query.ExpiryDate,6000,null,null,query.FrozenEmulatorPreviewOnly?"Frozen emulator preview fixture":"Live",at,mode=="Duplicate"?[row,row]:[row]));
 }
}
