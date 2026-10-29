using Reviewed = TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using System.Runtime.InteropServices;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using MessagePack;
using System.Reflection;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using System.Text.Json;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
if(args.Length==2 && args[0]=="--export-ownership") {
 using var fixtures=JsonDocument.Parse(await File.ReadAllTextAsync("TomasAI.IFM.Application.MarketData.Rust/qualification/ContextWireBridge/contexts.json"));
 var prepared=MessagePackSerializer.Deserialize<OptionPricingContext>(Convert.FromBase64String(fixtures.RootElement[0].GetProperty("context").GetString()!));
 var random=new Random(57019);var cases=new List<object>();
 for(var index=0;index<81;index++){
  var definitions=new List<WorkerOptionDefinition>();var candidates=new List<object>();var size=index==80?2048:1+index%17;
  for(var physicalLegIndex=0;physicalLegIndex<size;physicalLegIndex++){
   var name=((physicalLegIndex%4) switch{0=>"\U00010000",1=>"\uE000",2=>"Z",_=>"a"})+"-"+index+"-"+physicalLegIndex;
   var contract=prepared.Contract with{ContractId=name,InstrumentId=checked((uint)(100+physicalLegIndex)),RawSymbol="ESZ6 C"+physicalLegIndex,UnderlyingContractId="ES20261218",DefinitionDigest=new string((physicalLegIndex%2==0?'a':'B'),64)};
   var strike=(physicalLegIndex%7) switch{0=>0m,1=>1.0000m,2=>.0000000000000000000000000001m,3=>decimal.MaxValue,4=>decimal.MinValue,_=>new decimal(random.Next(),random.Next(),random.Next(),false,(byte)random.Next(0,29))};
   var call=physicalLegIndex%2==0;definitions.Add(new(prepared with{Contract=contract},strike,call));
   candidates.Add(new object[]{contract.ContractId,contract.MappingVersion,contract.DefinitionDigest,contract.Dataset,contract.PublisherId,contract.InstrumentId,contract.RawSymbol,contract.UnderlyingContractId,call?1:2,strike.ToString(System.Globalization.CultureInfo.InvariantCulture),checked((ulong)(contract.ExpirationUtc.UtcTicks-DateTime.UnixEpoch.Ticks)*100)});
  }
  cases.Add(new{candidates,digest=WorkerOptionChainRuntime.PhysicalDigest(definitions.ToImmutableArray())});
 }
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(cases));Console.WriteLine($"PASS exported {cases.Count} unchanged C# physical ownership hashes, including 2048 exact definitions, Unicode ordinal ordering and decimal boundaries.");return;
}
if(args.Length==2 && args[0]=="--export-coverage") {
 using var coveragePolicies=JsonDocument.Parse(await File.ReadAllTextAsync("TomasAI.IFM.Application.MarketData.Rust/qualification/ContextWireBridge/strategy-policy.json"));
 var coverageBase=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet>(Convert.FromBase64String(coveragePolicies.RootElement[0].GetProperty("content").GetString()!));
 var coverageDate=new DateOnly(2026,10,27);var coverageCalendar=new OptionPricingCalendar("fixture/v1","Eastern Standard Time",coverageDate,coverageDate.AddDays(40),new(18,0),[coverageDate]);
 var coverageCases=new List<object>();
 for(int coverageMode=0;coverageMode<21;coverageMode++) {
  var coveragePolicy=coverageBase;var coverageForward=coverageMode==10?0m:6000m;var coverageIv=coverageMode==3?0d:coverageMode==4?5.1:coverageMode==12?.02:.2;
  if(coverageMode==1)coveragePolicy=coveragePolicy with{Enabled=false};
  if(coverageMode==7)coveragePolicy=coveragePolicy with{BiasRows=coveragePolicy.BiasRows.Select((b,n)=>n==2?b with{MinimumDte=60,MaximumDte=90,PreferredDte=75}:b).ToImmutableArray()};
  if(coverageMode==8)coveragePolicy=coveragePolicy with{MaximumExpirations=1,BiasRows=coveragePolicy.BiasRows.Select((b,n)=>b with{MinimumDte=n==0?1:14,MaximumDte=n==0?7:30,PreferredDte=n==0?1:21}).ToImmutableArray()};
  if(coverageMode==11)coveragePolicy=coveragePolicy with{MaximumContracts=16};
  if(coverageMode==12)coveragePolicy=coveragePolicy with{MaximumContracts=44,MaximumExpirations=1};
  var coverageRows=new List<OptionDefinitionCandidate>();
  foreach(int days in new[]{1,7,14,21})foreach(var underlying in new[]{"ES20261218","ES20270319"})foreach(bool call in new[]{false,true})for(int strike=5500;strike<=6500;strike+=10) {
   var id=$"{underlying}-{days}-{(call?"C":"P")}-{strike}";var expiry=checked((ulong)(new DateTimeOffset(coverageDate.AddDays(coverageMode==6?days+730:days).ToDateTime(new(16,0)),TimeSpan.Zero).UtcTicks-DateTime.UnixEpoch.Ticks)*100);
   ulong? expiryNs=coverageMode==14?expiry+1:coverageMode==15?ulong.MaxValue:expiry;
   var definition=new OptionContractDefinition{MaturityDate=coverageDate.AddDays(days),Dataset="GLBX.MDP3",Ticker=coverageMode==16?"VX":"ES",Underlying=underlying,RawSymbol=id,Instrument=new(3,(uint)(coverageRows.Count+1)),StrikePrice=strike+(coverageMode==13?10000:0),Right=call?OptionRightSelection.Call:OptionRightSelection.Put,ExpirationTimestampNanoseconds=expiryNs};
   coverageRows.Add(new(id,"v1",new('a',64),definition));
  }
  if(coverageMode==5)coverageRows.Add(coverageRows[0]);if(coverageMode==17)coverageRows.Clear();
  if(coverageMode==18){var unicodeRows=coverageRows.Select((row,index)=>(row,index)).Where(p=>p.row.Definition.StrikePrice==6000 && p.row.Definition.MaturityDate==coverageDate.AddDays(14) && p.row.Definition.Underlying=="ES20261218").ToArray();coverageRows[unicodeRows[0].index]=unicodeRows[0].row with{ContractId="\U00010000"};coverageRows[unicodeRows[1].index]=unicodeRows[1].row with{ContractId="\uE000"};}
  Dictionary<string,decimal>? coveragePrices=coverageMode==9?new(){{"ES20261218",6000m}}:coverageMode==20?new(){{"ES20261218",5900m},{"ES20270319",6100m}}:null;
  var planner=new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionUniversePlanner();var steps=new List<object>();
  foreach(var increment in coverageMode==0?new[]{0m,1m,100m,-100m}:new[]{0m}) {
   var forward=coverageForward+increment;var valueDate=coverageMode==19?default:coverageDate;
   var planned=planner.Plan(coveragePolicy,valueDate,coverageRows.ToImmutableArray(),coverageMode!=2,forward,coverageIv,coverageCalendar,UsTreasuryPublicationCalendar.Default2026,UsTreasuryCurve.ConversionPolicy,coveragePrices);
   steps.Add(new{forward=forward.ToString(System.Globalization.CultureInfo.InvariantCulture),reason=planned.ReasonCode,plans=planned.Universes.Select(u=>new{scope=u.ScopeId,digest=u.ConfigurationDigest,expiration=u.MarketData.MaturityDate.DayNumber,underlying=u.MarketData.Options[0].Definition.Underlying,contracts=u.MarketData.Options.Select(o=>o.ContractId).ToArray()})});
  }
  coverageCases.Add(new{mode=coverageMode,parameters=MessagePackSerializer.Serialize(coveragePolicy),date=(coverageMode==19?default:coverageDate).DayNumber,complete=coverageMode!=2,iv=coverageIv,zone="Eastern Standard Time",prices=coveragePrices?.ToDictionary(x=>x.Key,x=>x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),rows=coverageRows.Select(c=>new{contract=c.ContractId,root=c.Definition.Ticker,underlying=c.Definition.Underlying,strike=c.Definition.StrikePrice.ToString(System.Globalization.CultureInfo.InvariantCulture),expiration=c.Definition.ExpirationTimestampNanoseconds}),steps});
 }
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(coverageCases));Console.WriteLine($"PASS exported {coverageCases.Count} complete C# universe scenarios including retained envelopes.");return;
}
if(args.Length==2 && args[0]=="--export-decimal-arithmetic") {
 decimal[] operands=[0m,0.0000m,1m,-1m,1.2m,6000.125m,decimal.MaxValue,decimal.MinValue,1e-28m,5e-28m,0.1000000000000000000000000000m,7922816251426433759354395033.5m];
 var arithmeticCases=new List<object>();
 void Capture(decimal left,decimal right){foreach(var op in new[]{"add","subtract","multiply"}){int[]? result;string? text;try{var value=op switch{"add"=>checked(left+right),"subtract"=>checked(left-right),_=>checked(left*right)};result=decimal.GetBits(value);text=value.ToString(System.Globalization.CultureInfo.InvariantCulture);}catch(OverflowException){result=null;text=null;}arithmeticCases.Add(new{left=decimal.GetBits(left),right=decimal.GetBits(right),op,result,text});}}
 foreach(var left in operands)foreach(var right in operands)Capture(left,right);
 ulong arithmeticRandom=0xd1342543de82ef95;uint NextWord(){arithmeticRandom=unchecked(arithmeticRandom*6364136223846793005+1442695040888963407);return (uint)(arithmeticRandom>>32);}
 for(int n=0;n<4096;n++){decimal Operand()=>new(unchecked((int)NextWord()),unchecked((int)NextWord()),unchecked((int)NextWord()),(NextWord()&1)!=0,(byte)(NextWord()%29));Capture(Operand(),Operand());}
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(arithmeticCases));Console.WriteLine($"PASS exported {arithmeticCases.Count} CLR decimal arithmetic outcomes.");return;
}
if(args.Length==2 && args[0]=="--export-strategy-policy") {
 var policyCases=new List<object>();
 for(int version=1;version<=8;version++) {
  var directions=Enum.GetValues<TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyMarketBias>().Select(direction=>new TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyBiasParameters{
   MarketBias=direction,MinimumDte=1,MaximumDte=30+version,PreferredDte=14,PutDelta=new(.1000m,.2m,.3m),CallDelta=new(.1m,.2m,.30m),
   PutWingWidths=[5.00m,10m],CallWingWidths=[5m,10.0m],TargetNetDelta=direction==TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyMarketBias.Bearish?-.1000m:.1000m,
   NetDeltaTolerance=.20m,MinimumNetCredit=.05m,MaximumNetDebit=version%2==0?1.250m:null,MaximumLegSpreadPoints=.25m,MaximumCandidatesPerSide=version}).ToImmutableArray();
  var policy=new TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet{ParameterSetId=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),Version=version,Name=version%2==0?"Policy <&\"\u6f22\u5b57":"Policy",StrategyDefinitionId=Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100"),StrategyDefinitionVersion=2,Enabled=true,BiasRows=directions,CacheRangeMultiplier=1+version*.5,MaximumContracts=2048,MaximumExpirations=3};
  policy.Validate();var preparedSnapshots=new List<object>();
  foreach(var direction in directions) {
   var policySnapshot=new MarketCompositionSnapshot(1,Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),"strategy-test","strategy-token","Weekly",Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100"),new(2026,10,27,12,0,0,TimeSpan.Zero),new(2026,10,27,12,0,1,TimeSpan.Zero),[],""){
    StrategyOptionChainParameters=policy,StrategyOptionChainBias=direction.MarketBias.ToString(),StrategyOptionChainValueDate=new(2026,10,27)};
   policySnapshot=policySnapshot with{Digest=PricingSemanticHash.Compute(policySnapshot)};
   preparedSnapshots.Add(new{direction=(int)direction.MarketBias,content=MessagePackSerializer.Serialize(policySnapshot),digest=policySnapshot.Digest});
  }
  var command=typeof(NativeMarketDataRuntime).GetMethod("RankingArguments",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[new NativeOwnerRanking(policy,directions[0].MarketBias,new(2026,10,27))]);
  policyCases.Add(new{command=JsonSerializer.SerializeToElement(command),content=MessagePackSerializer.Serialize(policy),hash=policy.Hash(),semantic=PricingSemanticHash.Compute(policy),snapshots=preparedSnapshots});
 }
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(policyCases));Console.WriteLine($"PASS exported {policyCases.Count} typed policy identities and 24 policySnapshot metadata cases.");return;
}
if(args.Length==2 && args[0]=="--export-candidates") {
 using var fixture=JsonDocument.Parse(await File.ReadAllTextAsync("TomasAI.IFM.Application.MarketData.Rust/qualification/ContextWireBridge/contexts.json"));
 var first=fixture.RootElement[0];var sample=first.GetProperty("compositions").EnumerateArray().Single(x=>x.GetProperty("mode").GetString()=="risk");
 var candidateBasis=MessagePackSerializer.Deserialize<MarketCompositionSnapshot>(Convert.FromBase64String(sample.GetProperty("content").GetString()!)).Instruments[0];
 var now=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero);var cases=new List<object>();
 for(int mode=0;mode<23;mode++)foreach(int maximum in new[]{1,4,32}) {
  var policy=new TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyBiasParameters {MinimumDte=1,MaximumDte=30,PreferredDte=14,
   PutDelta=new(.1m,.2m,.3m),CallDelta=new(.1m,.2m,.3m),PutWingWidths=[5m,10m],CallWingWidths=[5m,10m],MaximumQuoteAgeMilliseconds=1000,MaximumQuoteSkewMilliseconds=250,MaximumLegSpreadPoints=.25m,MaximumCandidatesPerSide=maximum};
  var candidateRows=new List<CompositionInstrumentSnapshot>();
  foreach(bool call in new[]{false,true})for(int k=-4;k<=4;k++) {
   var id=$"{(call?"C":"P")}{k+4:D2}";
   var quote=candidateBasis.Instrument.Quote! with {ContractId=id,Bid=10m,Ask=10.25m,BidSize=2,AskSize=2,EventAtUtc=now.AddMilliseconds(-100),ReceivedAtUtc=now.AddMilliseconds(-50)};
   var future=candidateBasis.Instrument.Underlying! with{EventAtUtc=now.AddMilliseconds(-100),ReceivedAtUtc=now.AddMilliseconds(-50)};
   var pricing=candidateBasis.Instrument.Pricing! with{ValidUntilUtc=now.AddSeconds(1)};
   OptionPricingValue? valuation=candidateBasis.Valuation! with{Delta=(call?1:-1)*(.2+k*.025)};
   OptionPricingQuote? q=quote,u=future;OptionPricingContext? context=pricing;
   if(k==0)switch(mode) {
    case 1:context=null;break;case 2:valuation=null;break;case 3:q=null;break;case 4:u=null;break;
    case 5:context=pricing with{ValidUntilUtc=now};break;case 6:q=quote with{Bid=0};break;case 7:q=quote with{Ask=9};break;
    case 8:q=quote with{Ask=10.250000001m};break;case 9:q=quote with{BidSize=0};break;case 10:q=quote with{ReceivedAtUtc=now.AddTicks(1)};break;
    case 11:q=quote with{EventAtUtc=now.AddMilliseconds(-1000).AddTicks(-1)};break;case 12:u=future with{ReceivedAtUtc=now.AddMilliseconds(-1000).AddTicks(-1)};break;
    case 13:u=future with{EventAtUtc=now.AddMilliseconds(-350).AddTicks(-1)};break;case 14:q=quote with{EventAtUtc=now.AddMilliseconds(100)};break;
    case 15:context=pricing with{Contract=pricing.Contract with{ExpirationUtc=pricing.Contract.ExpirationUtc.AddTicks(1)}};break;
    case 16:context=pricing with{Contract=pricing.Contract with{UnderlyingContractId="other"}};break;
    case 17:valuation=valuation! with{Delta=call?Math.BitIncrement(.1):-Math.BitIncrement(.1)};break;
    case 18:valuation=valuation! with{Delta=call?Math.BitDecrement(.3):-Math.BitDecrement(.3)};break;
    case 19:q=quote with{EventAtUtc=now.AddMilliseconds(-500),ReceivedAtUtc=now.AddMilliseconds(-450)};u=future with{EventAtUtc=now.AddMilliseconds(-500),ReceivedAtUtc=now.AddMilliseconds(-450)};break;
    case 20:q=quote with{BidSize=1,AskSize=1};break;case 21:q=quote with{EventAtUtc=now.AddMilliseconds(-1000),ReceivedAtUtc=now.AddMilliseconds(-1000)};u=future with{EventAtUtc=now.AddMilliseconds(-1000),ReceivedAtUtc=now.AddMilliseconds(-1000)};break;
   }
   var instrument=candidateBasis.Instrument with{ContractId=id,Strike=6000+k*5,IsCall=call,Quote=q,Underlying=u,Pricing=context};
   candidateRows.Add(new(instrument,valuation));
   if(mode==22 && k==1)candidateRows.Add(new(instrument with{ContractId=id+"-duplicate"},valuation));
  }
  object? Quote(OptionPricingQuote? q)=>q is null?null:new{bid=q.Bid.ToString(System.Globalization.CultureInfo.InvariantCulture),ask=q.Ask.ToString(System.Globalization.CultureInfo.InvariantCulture),bidSize=q.BidSize.ToString(System.Globalization.CultureInfo.InvariantCulture),askSize=q.AskSize.ToString(System.Globalization.CultureInfo.InvariantCulture),at=q.EventAtUtc.UtcTicks-DateTime.UnixEpoch.Ticks,received=q.ReceivedAtUtc.UtcTicks-DateTime.UnixEpoch.Ticks};
  var selected=TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCandidatePreparation.Select(candidateRows.ToImmutableArray(),policy,now).Select(x=>x.Instrument.ContractId).ToArray();
  cases.Add(new{mode,maximum,at=now.UtcTicks-DateTime.UnixEpoch.Ticks,selected,rows=candidateRows.Select(row=>new{id=row.Instrument.ContractId,underlying=row.Instrument.Pricing?.Contract.UnderlyingContractId??"ES20261218",expiration=(row.Instrument.Pricing?.Contract.ExpirationUtc??candidateBasis.Instrument.Pricing!.Contract.ExpirationUtc).UtcTicks-DateTime.UnixEpoch.Ticks,strike=row.Instrument.Strike!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),call=row.Instrument.IsCall!.Value,delta=row.Valuation?.Delta,until=row.Instrument.Pricing?.ValidUntilUtc.UtcTicks-DateTime.UnixEpoch.Ticks,quote=Quote(row.Instrument.Quote),future=Quote(row.Instrument.Underlying)})});
 }
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(cases));Console.WriteLine($"PASS exported {cases.Count} actual C# candidate policy cases.");return;
}
if(args.Length==2&&args[0]=="--export-reviewed-options"){
 var reviewedAt=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero);var reviewedExpiry=reviewedAt.AddDays(14);
 var reviewedBasis=new Reviewed.FuturesOptionContractReadModel("ES20261110C6000","fixture","ES","fixture-option","FOP","USD","XCME","50",new(2026,11,10),6000,"Call"){
  StrikePriceDecimal=6000,SchemaVersion=1,ReviewState=Reviewed.ReferenceReviewState.Reviewed,Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=43,RawSymbol="fixture-option",
  DefinitionTimestampUtc=reviewedAt,DefinitionDigest=new('a',64),RawDefinitionReference="fixture/raw",ExpirationUtc=reviewedExpiry,LastTradingUtc=reviewedExpiry,ExchangeTimeZoneId="America/New_York",
  SettlementStyle=Reviewed.ReferenceSettlementStyle.DeliveryOfFuture,MultiplierValue=50,PriceScale=1000000000,TickSize=.25m,CalendarVersion="fixture/v1",MappingVersion="fixture/v1",EvidenceId="fixture",
  EffectiveFromUtc=reviewedAt.AddDays(-30),EffectiveUntilUtc=reviewedAt.AddDays(45),UnderlyingContractId="ES20261218",UnderlyingAssetType=Reviewed.ReferenceAssetType.Futures,
  OptionRight=Reviewed.ReferenceOptionRight.Call,ExerciseStyle=Reviewed.ReferenceExerciseStyle.European,PremiumStyle=Reviewed.ReferencePremiumStyle.PremiumPaid,
  PremiumTickRule=Reviewed.ReferencePremiumTickRule.Fixed,TickRuleVersion="fixture-tick/v1",DayCount=Reviewed.ReferenceDayCount.Actual365Fixed,UnderlyingInstrumentId=42,UnderlyingPublisherId=3};
 var reviewedInputs=new List<(string Name,Reviewed.FuturesOptionContractReadModel Value)>{("base",reviewedBasis)};
 foreach(var reviewedProperty in typeof(Reviewed.FuturesOptionContractReadModel).GetProperties().Where(p=>p.CanWrite&&p.GetCustomAttribute<MessagePack.KeyAttribute>() is not null)){
  var reviewedCopy=reviewedBasis with{};var reviewedType=reviewedProperty.PropertyType;
  reviewedProperty.SetValue(reviewedCopy,reviewedType.IsValueType?Activator.CreateInstance(reviewedType):null);reviewedInputs.Add((reviewedProperty.Name+"/default",reviewedCopy));
  if(reviewedType==typeof(string)){reviewedCopy=reviewedBasis with{};reviewedProperty.SetValue(reviewedCopy," ");reviewedInputs.Add((reviewedProperty.Name+"/space",reviewedCopy));}
 }
 reviewedInputs.AddRange(new (string,Reviewed.FuturesOptionContractReadModel)[]{
  ("strike-nan",reviewedBasis with{StrikePrice=double.NaN}),("strike-infinity",reviewedBasis with{StrikePrice=double.PositiveInfinity}),("strike-conflict",reviewedBasis with{StrikePriceDecimal=6001}),
  ("legacy-translate",reviewedBasis with{StrikePriceDecimal=null}),("bad-zone",reviewedBasis with{ExchangeTimeZoneId="invalid/zone"}),("windows-zone",reviewedBasis with{ExchangeTimeZoneId="Eastern Standard Time"}),
  ("bad-multiplier",reviewedBasis with{Multiplier="-50"}),("signed-multiplier",reviewedBasis with{Multiplier="+50"}),("decimal-multiplier",reviewedBasis with{Multiplier="50.00"}),
  ("fractional-multiplier",reviewedBasis with{Multiplier=".50",MultiplierValue=.5m}),("invalid-digest",reviewedBasis with{DefinitionDigest=new('g',64)}),
  ("time-offset",reviewedBasis with{DefinitionTimestampUtc=reviewedAt.ToOffset(TimeSpan.FromHours(1))}),("min-time",reviewedBasis with{DefinitionTimestampUtc=DateTimeOffset.MinValue}),
  ("max-time",reviewedBasis with{EffectiveUntilUtc=DateTimeOffset.MaxValue}),("last-after-expiry",reviewedBasis with{LastTradingUtc=reviewedExpiry.AddTicks(1)}),
  ("bad-interval",reviewedBasis with{EffectiveUntilUtc=reviewedBasis.EffectiveFromUtc}),("cme-premium",reviewedBasis with{PremiumStyle=Reviewed.ReferencePremiumStyle.FuturesStyleVariation}),
  ("other-premium",reviewedBasis with{Symbol="VX",PremiumStyle=Reviewed.ReferencePremiumStyle.FuturesStyleVariation}),("wrong-right",reviewedBasis with{OptionType="Put"}),
  ("cutoff-after-expiry",reviewedBasis with{ExerciseCutoffUtc=reviewedExpiry.AddTicks(1)}),("cutoff-offset",reviewedBasis with{ExerciseCutoffUtc=reviewedAt.ToOffset(TimeSpan.FromHours(1))}),
  ("zero-tick",reviewedBasis with{TickSize=0}),("negative-scale",reviewedBasis with{PriceScale=-1}),("non-ascii-root",reviewedBasis with{Symbol="E?",PremiumStyle=Reviewed.ReferencePremiumStyle.FuturesStyleVariation})});
 var reviewedFixtures=reviewedInputs.Select(reviewedEntry=>{
  byte[]? reviewedConvention=null;try{reviewedConvention=MessagePackSerializer.Serialize(ReviewedOptionConvention.From(reviewedEntry.Value));}catch(ArgumentException){}
  return new{name=reviewedEntry.Name,command=NativeReferenceProjection.Option(reviewedEntry.Value),content=MessagePackSerializer.Serialize(reviewedEntry.Value),errors=Reviewed.FuturesReferenceQualification.Errors(reviewedEntry.Value),convention=reviewedConvention};});
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(reviewedFixtures));Console.WriteLine($"PASS exported {reviewedInputs.Count} complete raw option qualification cases.");return;
}
if(args.Length==3 && args[0]=="--export-cache-reads") {
 using var cacheFixtures=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));var cacheOracle=new List<object>();
 foreach(var cacheRow in cacheFixtures.RootElement.EnumerateArray()){
  var cacheFixture=cacheRow.GetProperty("compositions").EnumerateArray().Single(x=>x.GetProperty("mode").GetString()=="risk");
  var cacheSnapshot=MessagePackSerializer.Deserialize<MarketCompositionSnapshot>(Convert.FromBase64String(cacheFixture.GetProperty("content").GetString()!));
  cacheSnapshot=cacheSnapshot with{Instruments=cacheSnapshot.Instruments.Select(row=>row with{Valuation=row.Valuation! with{ContextDigest=new string('a',64)}}).ToImmutableArray()};
  cacheSnapshot=cacheSnapshot with{Digest=PricingSemanticHash.Compute(cacheSnapshot with{Digest=""})};
  var cacheAt=cacheSnapshot.EvaluatedAtUtc;var cacheDate=DateOnly.FromDateTime(cacheAt.UtcDateTime);var cacheCases=new List<object>();
  foreach(var cacheMode in new[]{"valid","blank-scope","default-date","bad-horizon","nonutc-deadline","expired-deadline","blank-config","bad-root","negative-version","missing-strategy-version","too-many-required","duplicate-required","blank-required","default-required","invalid-age-low","invalid-age-high","invalid-skew","fund","wrong-generation","missing-scope","wrong-date","wrong-config","wrong-horizon","wrong-strategy","expired","future","coverage","empty","missing-quote","missing-underlying","missing-context","wrong-quote-id","wrong-underlying-id","quote-generation","underlying-generation","stale-quote","future-quote","future-received","invalid-bid","crossed-quote","zero-size","quote-offset","underlying-offset","zero-underlying","incoherent","age-boundary","lead-boundary"}){
   var cacheNow=cacheMode=="expired"?cacheAt.AddSeconds(2):cacheMode=="future"?cacheAt.AddTicks(-1):cacheAt;
   var cachePublished=cacheMode=="empty"?cacheSnapshot with{Instruments=[]}:cacheSnapshot;
   if(cacheMode!="empty"){
    var cacheItem=cachePublished.Instruments[0].Instrument;var cacheQuote=cacheItem.Quote!;var cacheUnderlying=cacheItem.Underlying!;
    cacheItem=cacheMode switch{
     "missing-quote"=>cacheItem with{Quote=null},"missing-underlying"=>cacheItem with{Underlying=null},"missing-context"=>cacheItem with{Pricing=null},
     "wrong-quote-id"=>cacheItem with{Quote=cacheQuote with{ContractId="wrong"}},"wrong-underlying-id"=>cacheItem with{Underlying=cacheUnderlying with{ContractId="wrong"}},
     "quote-generation"=>cacheItem with{Quote=cacheQuote with{GenerationId=Guid.Empty}},"underlying-generation"=>cacheItem with{Underlying=cacheUnderlying with{GenerationId=Guid.Empty}},
     "stale-quote"=>cacheItem with{Quote=cacheQuote with{EventAtUtc=cacheAt.AddMilliseconds(-1001)}},"future-quote"=>cacheItem with{Quote=cacheQuote with{EventAtUtc=cacheAt.AddMilliseconds(124)}},
     "future-received"=>cacheItem with{Quote=cacheQuote with{ReceivedAtUtc=cacheAt.AddTicks(1)}},"invalid-bid"=>cacheItem with{Quote=cacheQuote with{Bid=-1}},
     "crossed-quote"=>cacheItem with{Quote=cacheQuote with{Ask=cacheQuote.Bid-1}},"zero-size"=>cacheItem with{Quote=cacheQuote with{BidSize=0}},
     "quote-offset"=>cacheItem with{Quote=cacheQuote with{EventAtUtc=cacheQuote.EventAtUtc.ToOffset(TimeSpan.FromHours(1))}},
     "underlying-offset"=>cacheItem with{Underlying=cacheUnderlying with{ReceivedAtUtc=cacheUnderlying.ReceivedAtUtc.ToOffset(TimeSpan.FromHours(1))}},
     "zero-underlying"=>cacheItem with{Underlying=cacheUnderlying with{Bid=0}},
     "incoherent"=>cacheItem with{Quote=cacheQuote with{EventAtUtc=cacheAt.AddMilliseconds(-251)},Underlying=cacheUnderlying with{EventAtUtc=cacheAt}},
     "age-boundary"=>cacheItem with{Quote=cacheQuote with{EventAtUtc=cacheAt.AddMilliseconds(-1000)}},"lead-boundary"=>cacheItem with{Quote=cacheQuote with{EventAtUtc=cacheAt.AddMilliseconds(123)}},_=>cacheItem};
    cachePublished=cachePublished with{Instruments=[cachePublished.Instruments[0] with{Instrument=cacheItem}]};
   }
   cachePublished=cachePublished with{Digest=PricingSemanticHash.Compute(cachePublished with{Digest=""})};
   using var preparedCache=new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache(new FixedTime(cacheNow));preparedCache.Admit(cachePublished.GenerationId);
   if(!preparedCache.Publish(cachePublished,cacheDate,"fixture-config",7))throw new InvalidDataException("Cache oracle publication failed.");
   var cacheRequest=new TomasAI.IFM.Application.MarketData.Contracts.OptionChainSnapshotRequest(cacheSnapshot.ScopeId,cacheSnapshot.GenerationId,cacheDate,"Daily",cacheAt.AddMinutes(1),1000,250){ConfigurationDigest="fixture-config"};
   cacheRequest=cacheMode switch{
    "blank-scope"=>cacheRequest with{ScopeId=" "},"default-date"=>cacheRequest with{ValueDate=default},"bad-horizon"=>cacheRequest with{Horizon="Other"},
    "nonutc-deadline"=>cacheRequest with{DeadlineUtc=cacheRequest.DeadlineUtc.ToOffset(TimeSpan.FromHours(1))},"expired-deadline"=>cacheRequest with{DeadlineUtc=cacheAt},
    "blank-config"=>cacheRequest with{ConfigurationDigest=" "},"bad-root"=>cacheRequest with{InstrumentRoot="VX"},"negative-version"=>cacheRequest with{StrategyDefinitionVersion=-1},
    "missing-strategy-version"=>cacheRequest with{StrategyDefinitionId=new Guid(1,0,0,new byte[8])},"too-many-required"=>cacheRequest with{RequiredContractIds=["a","b","c","d","e"]},
    "duplicate-required"=>cacheRequest with{RequiredContractIds=["a","a"]},"blank-required"=>cacheRequest with{RequiredContractIds=[" "]},"default-required"=>cacheRequest with{RequiredContractIds=default},
    "invalid-age-low"=>cacheRequest with{MaximumQuoteAgeMilliseconds=0},"invalid-age-high"=>cacheRequest with{MaximumQuoteAgeMilliseconds=5001},"invalid-skew"=>cacheRequest with{MaximumQuoteSkewMilliseconds=2001},
    "fund"=>cacheRequest with{FundPortfolioParameterSchema="unsupported"},"wrong-generation"=>cacheRequest with{GenerationId=new Guid(1,0,0,new byte[8])},"missing-scope"=>cacheRequest with{ScopeId="missing"},
    "wrong-date"=>cacheRequest with{ValueDate=cacheDate.AddDays(1)},"wrong-config"=>cacheRequest with{ConfigurationDigest="wrong"},"wrong-horizon"=>cacheRequest with{Horizon="Weekly"},
    "wrong-strategy"=>cacheRequest with{StrategyDefinitionId=new Guid(1,0,0,new byte[8]),StrategyDefinitionVersion=1},"coverage"=>cacheRequest with{RequiredContractIds=["missing"]},_=>cacheRequest};
   var cacheRead=preparedCache.TryGetSnapshot(cacheRequest);
   cacheCases.Add(new{mode=cacheMode,now=cacheNow.UtcTicks-DateTime.UnixEpoch.Ticks,request=new{scope=cacheRequest.ScopeId,generation=cacheRequest.GenerationId.ToByteArray().Select(b=>(int)b).ToArray(),date=cacheRequest.ValueDate.DayNumber,
    horizon=cacheRequest.Horizon,deadline=cacheRequest.DeadlineUtc.UtcTicks-DateTime.UnixEpoch.Ticks,deadline_offset=(int)cacheRequest.DeadlineUtc.Offset.TotalMinutes,configuration=cacheRequest.ConfigurationDigest,
    strategy=cacheRequest.StrategyDefinitionId==Guid.Empty?"":cacheRequest.StrategyDefinitionId.ToString(),strategy_version=cacheRequest.StrategyDefinitionVersion,root=cacheRequest.InstrumentRoot,
    required=cacheRequest.RequiredContractIds.IsDefault?null:cacheRequest.RequiredContractIds.ToArray(),fund_schema=cacheRequest.FundPortfolioParameterSchema,age=cacheRequest.MaximumQuoteAgeMilliseconds,skew=cacheRequest.MaximumQuoteSkewMilliseconds},
    outcome=(int)cacheRead.Outcome,reason=cacheRead.ReasonCode,version=cacheRead.ChainVersion});
  }
  cacheOracle.Add(new{name=cacheRow.GetProperty("name").GetString(),cases=cacheCases});
 }
 await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(cacheOracle));Console.WriteLine($"PASS exported {cacheOracle.Count*47} existing C# cache request outcomes.");return;
}
if(args.Length==2 && args[0]=="--export-exact-strike") {
 var strikeNumbers=new List<double>{0d,-0d,double.NaN,double.PositiveInfinity,double.NegativeInfinity,double.MaxValue,double.Epsilon,
  1e-29,5e-29,1.5e-28,1e-28,1e28,(double)decimal.MaxValue,6000d,6000.25,6000.1,0.1,1.000000000000001};
 foreach(var strikeCenter in new[]{0.1,1e-28,1e28,(double)decimal.MaxValue,6000.1,6000.25})for(int n=-4;n<=4;n++){
  var candidate=strikeCenter;for(int k=0;k<Math.Abs(n);k++)candidate=n<0?Math.BitDecrement(candidate):Math.BitIncrement(candidate);strikeNumbers.Add(candidate);}
 ulong strikeRandom=0x6a09e667f3bcc909;
 for(int n=0;n<8192;n++){strikeRandom=unchecked(strikeRandom*6364136223846793005+1442695040888963407);strikeNumbers.Add(BitConverter.Int64BitsToDouble(unchecked((long)strikeRandom)));}
 var strikeCases=new List<object>();
 foreach(var strikeLegacy in strikeNumbers)foreach(var strikeExact in new decimal?[]{null,0,-1,0.1m,6000.1m,6000.25m,decimal.MaxValue}) {
  decimal? strikeResult;try{strikeResult=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel{StrikePrice=strikeLegacy,StrikePriceDecimal=strikeExact}.GetExactStrikePrice();}catch(InvalidDataException){strikeResult=null;}
  strikeCases.Add(new{input=unchecked((ulong)BitConverter.DoubleToInt64Bits(strikeLegacy)).ToString("x16"),exact=strikeExact?.ToString(System.Globalization.CultureInfo.InvariantCulture),result=strikeResult?.ToString(System.Globalization.CultureInfo.InvariantCulture)});
 }
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(strikeCases));Console.WriteLine($"PASS exported {strikeCases.Count} exact legacy/exact strike cases.");return;
}
if(args.Length==2 && args[0]=="--export-double-decimal") {
 var numbers=new List<double>{0d,-0d,double.NaN,double.PositiveInfinity,double.NegativeInfinity,double.MaxValue,double.Epsilon,
  1e-29,5e-29,1.5e-28,1e-28,1e28,(double)decimal.MaxValue,1e15-1,1e15,0.1,0.15,0.3,1.000000000000001};
 foreach(var value in new[]{0.1,0.15,0.3,0.999999999999999,1e-28,5e-29,1e15,1e28,(double)decimal.MaxValue})
  for(int n=-4;n<=4;n++){var candidate=value;for(int k=0;k<Math.Abs(n);k++)candidate=n<0?Math.BitDecrement(candidate):Math.BitIncrement(candidate);numbers.Add(candidate);numbers.Add(-candidate);}
 ulong random=0x9e3779b97f4a7c15;
 for(int n=0;n<8192;n++){random=unchecked(random*6364136223846793005+1442695040888963407);numbers.Add(BitConverter.Int64BitsToDouble(unchecked((long)random)));}
 var cases=numbers.Select(value=>{int[]? bits;try{bits=decimal.GetBits((decimal)value);}catch(OverflowException){bits=null;}return new{input=unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("x16"),bits};});
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(cases));Console.WriteLine($"PASS exported {numbers.Count} CLR double-to-decimal boundary cases.");return;
}
if(args.Length==2 && args[0]=="--verify-native-cache") {
 using var document=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
 var frames=document.RootElement.GetProperty("frames").EnumerateArray().Select(x=>
  MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublicationFrame>(Convert.FromBase64String(x.GetString()!),TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options)).ToArray();
 var assembler=new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublicationAssembler();
 TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublication? result=null;
 foreach(var frame in frames) result=assembler.Accept(frame);
 if(result is null || !MessagePackSerializer.Serialize(result).SequenceEqual(Convert.FromBase64String(document.RootElement.GetProperty("content").GetString()!)))
  throw new InvalidDataException("Native cache chunks did not reconstruct the exact existing C# publication.");
 if(PricingSemanticHash.Compute(result.Snapshot with{Digest=""})!=document.RootElement.GetProperty("digest").GetString())
  throw new InvalidDataException("Native cache semantic digest differs from existing C# computation.");
 if(frames.Length<2 || frames.Any(x=>x.Bytes.Length>65536))throw new InvalidDataException("Multi-chunk boundary was not exercised.");
 foreach(var defect in new[]{"missing","generation","digest","payload"}) {
  var invalid=new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublicationAssembler();
  TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublication? accepted=null;
  for(int i=0;i<frames.Length;i++) {
   if(defect=="missing"&&i==0)continue;
   var frame=frames[i];
   if(i==frames.Length-1) frame=defect switch {
    "generation"=>frame with{GenerationId=Guid.NewGuid()},"digest"=>frame with{Digest=new string('0',64)},
    "payload"=>frame with{Bytes=frame.Bytes.Select((b,index)=>index==0?(byte)(b^1):b).ToArray()},_=>frame};
   accepted=invalid.Accept(frame);
  }
  if(accepted is not null)throw new InvalidDataException("Corrupted native cache publication accepted: "+defect);
 }
 Console.WriteLine($"PASS actual C# assembler: {frames.Length} native chunks, 2048 contracts, exact wire/hash; missing/generation/digest/payload rejected.");
 return;
}
if(args.Length<3)throw new ArgumentException("Context fixture regeneration requires output paths and the actual native DLL for all pricing cases.");
var at=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero).AddTicks(1);
var basis=new OptionPricingConvention {SchemaVersion=3,ContractId="ES20261110C6000",Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=42,RawSymbol="fixture",Root="ES",Exchange="XCME",Currency="USD",
    UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=new(2026,11,10,16,0,0,TimeSpan.Zero),LastTradingUtc=new(2026,11,10,16,0,0,TimeSpan.Zero),
    DayCount=PricingDayCount.Actual365Fixed,CalendarVersion="fixture/v1",Multiplier=50,TickSize=.25m,TickRuleVersion="fixed/v1",DefinitionDigest=new('a',64),MappingVersion="fixture/v1",EvidenceId="fixture",
    EffectiveFromUtc=new(2025,1,1,0,0,0,TimeSpan.Zero),EffectiveUntilUtc=new(2027,1,1,0,0,0,TimeSpan.Zero),PremiumTickRule=OptionPremiumTickRule.Fixed,PremiumStyle=OptionPremiumStyle.PaidUpfront,UnderlyingKind=PricingUnderlyingKind.Futures,Strike=6000,Right=PricingOptionRight.Call};

var generation=Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
var dates=Enumerable.Range(0,60).Select(i=>new DateOnly(2026,10,1).AddDays(i)).ToImmutableArray();
var snapshot=new TreasuryCurveSnapshot(new(2026,10,26),[new(TreasuryTenor.OneMonth,4.0000m),new(TreasuryTenor.TwoMonth,4.10m),new(TreasuryTenor.ThreeMonth,4.2m)],at.AddMinutes(-1),UsTreasuryCurve.Source);
using var client=new HttpClient();using var source=new UsTreasuryCurve(client);
await using var nativeRuntime=args.Length>2?new NativeMarketDataRuntime(Path.GetFullPath(args[2])):null;
var rows=new List<object>();
foreach(var schema in new[]{1,2,3}) foreach(var exercise in new[]{OptionExerciseStyle.European,OptionExerciseStyle.American}) foreach(var fraction in new long[]{0,1234567}) {
 if(schema<3&&exercise!=OptionExerciseStyle.European)continue;
 var contract=basis with{SchemaVersion=schema,ExerciseStyle=exercise,PremiumStyle=schema<3?OptionPremiumStyle.FuturesStyle:OptionPremiumStyle.PaidUpfront,PremiumTickRule=schema==1?OptionPremiumTickRule.Unspecified:OptionPremiumTickRule.Fixed};
 var calendar=new OptionPricingCalendar("fixture/v1","Eastern Standard Time",new(2026,10,1),new(2026,11,29),new TimeOnly(18,0).Add(TimeSpan.FromTicks(fraction)),dates);
 var count=OptionPricingQualification.CountTradingDays(calendar,contract,at);
 var rate=source.GetContinuouslyCompoundedAnnualRate(snapshot,TreasuryRateConversion.SelectTenor(count)!.Value,UsTreasuryCurve.ConversionPolicy).Value!;
 var validUntil=at.AddHours(2);
 var context=new OptionPricingContext(contract,calendar,rate,validUntil,generation,exercise==OptionExerciseStyle.American?"AmericanFutures.CRR.Managed/v1":"Black76.Managed/v1",5000,2000,"publication/v1"){MaximumSourceClockLeadMilliseconds=123};
 var publication=new TreasuryPublicationPolicy("publication/v1",at.AddHours(-1),validUntil,[new(new(2026,10,26),at.AddHours(-1))]);
 var imported=new NativeReviewedScopeReferences(calendar,publication,UsTreasuryCurve.ConversionPolicy,[new(contract,7,6000m,true,true)],new OptionPricingRefreshPolicy(),123,false);
 var payload=typeof(NativeMarketDataRuntime).GetMethod("ReviewedScopePayload",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[imported,Array.Empty<NativeRegistration>(),Array.Empty<OptionChainStatisticsSnapshot>()]);
 var evidenceSource=new OptionTradeSource(contract.Dataset,contract.PublisherId,contract.InstrumentId,contract.ContractId,new(2026,10,27),10.2500m,3,77,1793102400123456789,1793102400123456999,generation,contract.RawSymbol);
 var evidenceQuote=new OptionPricingQuote(contract.UnderlyingContractId,6000m,6000.25m,2m,3m,at.AddTicks(-2),at.AddTicks(-1),76,generation);
 var fullGreeks=new OptionPricingValue(.2,.3,.004,-1.2,4.5,-.1,10.25,.04,"trade-digest");
 var failure=new OptionPricingFailure("StaleData","Underlying",contract.ContractId,"Source-time reference qualification has expired.");
 var successful=new OptionTradeEvidence(evidenceSource,context,evidenceQuote,at,fullGreeks,null);
 var failed=new OptionTradeEvidence(evidenceSource,context,evidenceQuote,at,null,failure);
 var missing=new OptionTradeEvidence(evidenceSource,null,null,at,null,failure);
 successful.Validate();failed.Validate();missing.Validate();
 var calculations=new List<object>();
 if(nativeRuntime is not null) {
  var tradePricing=new RustOptionTradePricing(nativeRuntime);
  foreach(var mode in new[]{"success","missing","empty","stale","negative","unbracketed"}) {
   var calculationSource=evidenceSource with{EventNanoseconds=(at.UtcTicks-DateTime.UnixEpoch.Ticks-2)*100,ReceiveNanoseconds=(at.UtcTicks-DateTime.UnixEpoch.Ticks-1)*100};
   if(mode=="empty")calculationSource=calculationSource with{Size=0};
   if(mode=="stale")calculationSource=calculationSource with{EventNanoseconds=calculationSource.EventNanoseconds-60_000_000_000};
   if(mode=="negative")calculationSource=calculationSource with{Price=-1m};
   if(mode=="unbracketed")calculationSource=calculationSource with{Price=50000m};
   var writer=new EvidenceCapture();
   var tradeResult=await tradePricing.CalculateAndRetainAsync(calculationSource,contract.UnderlyingContractId,context,mode=="missing"?null:evidenceQuote,6000m,true,at,writer,TimeSpan.FromMinutes(2));
   // Actual source-reader ingress divides integer DBN nanounits as Decimal;
   // constructor fixtures deliberately retain scales such as 10.2500 instead.
   var ingressSource=calculationSource with{Price=checked((long)(calculationSource.Price*1_000_000_000m))/1_000_000_000m};
   var ingressWriter=new EvidenceCapture();
   await tradePricing.CalculateAndRetainAsync(ingressSource,contract.UnderlyingContractId,context,mode=="missing"?null:evidenceQuote,6000m,true,at,ingressWriter,TimeSpan.FromMinutes(2));
   calculations.Add(new{mode,tradeGreeks=MessagePackSerializer.Serialize(NativeQueryGreeks.FromSnapshot(tradeResult)),source=MessagePackSerializer.Serialize(calculationSource),content=MessagePackSerializer.Serialize(writer.Value!),ingressContent=MessagePackSerializer.Serialize(ingressWriter.Value!)});
  }
 }
 var risks=new List<object>();
 var optionQuote=evidenceQuote with{ContractId=contract.ContractId,Bid=10.25m,Ask=10.25m,BidSize=3,AskSize=3,Sequence=77};
 var selections=new List<object>();
 foreach(var age in new[]{100,5000,60000}) {
  var ivOption=optionQuote with{EventAtUtc=at.AddSeconds(-1),ReceivedAtUtc=at.AddSeconds(-1).AddTicks(1)};
  var ivUnderlying=evidenceQuote with{EventAtUtc=at.AddSeconds(-1).AddTicks(-1),ReceivedAtUtc=at.AddSeconds(-1)};
  var iv=new ChainIvObservation(.2,context,ivUnderlying,ivOption,at.AddMilliseconds(-900),"OptionRefresh/TestDefaults/v1");
  var calculated=new ChainSelectionCalculation(context,evidenceQuote,optionQuote,iv,10.25,.3,at,null);
  var selected=OptionSelectionValue.From(calculated,age);
  selections.Add(new{age,content=MessagePackSerializer.Serialize(selected),digest=PricingSemanticHash.Compute(selected)});
 }
 foreach(var mode in new[]{"success","pending","stale","underlying","numerical","quote","expired"}) {
  var frameType=typeof(NativeMarketDataRuntime).Assembly.GetType("TomasAI.IFM.Application.MarketData.Rust.Interop.NativeScopeCalculation",true)!;
  object frame=Activator.CreateInstance(frameType)!;
  void Set(string field,object value)=>frameType.GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(frame,value);
  Set("Size",640u);Set("Flags",mode is "success" or "stale" or "expired"?5u:17u);Set("ContextLow",123ul);Set("PhysicalLow",77ul);
  Set("At",at.UtcTicks-DateTime.UnixEpoch.Ticks);Set("Time",.04);
  var quoteInput=typeof(NativeMarketDataRuntime).GetMethod("QuoteInput",BindingFlags.NonPublic|BindingFlags.Static)!;
  Set("Underlying",mode=="underlying"?Activator.CreateInstance(frameType.GetField("Underlying",BindingFlags.NonPublic|BindingFlags.Instance)!.FieldType)!:quoteInput.Invoke(null,[evidenceQuote])!);
  Set("Option",quoteInput.Invoke(null,[optionQuote])!);
  uint kind=mode switch{"pending"=>6u,"underlying"=>1u,"numerical"=>4u,"quote"=>3u,_=>0u};
  Set("FailureKind",kind);Set("FailureCode",mode is "numerical" or "quote"?4u:0u);
  var pointer=Marshal.AllocHGlobal(640);
  try{Marshal.StructureToPtr(frame,pointer,false);var offset=Marshal.OffsetOf(frameType,"Values").ToInt32();
   var values=new[]{10.25,.2,.3,.004,-1.2,4.5,-.1};for(int i=0;i<values.Length;i++)Marshal.WriteInt64(pointer,offset+i*8,BitConverter.DoubleToInt64Bits(values[i]));
   frame=Marshal.PtrToStructure(pointer,frameType)!;
  }finally{Marshal.FreeHGlobal(pointer);}
  var currentAt=mode=="stale"?at.AddMinutes(1):mode=="expired"?validUntil:at;
  var prepared=new PreparedOptionPricingContext(Guid.NewGuid(),context,7,6000,true,true,at.AddMinutes(1));
  var result=typeof(NativeOptionPricingCache).GetMethod("Risk",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[frame,prepared,currentAt])!;
  var riskResult=(OptionPricingPassResult)result;var g=riskResult.Value;
  OptionGreeksSnapshot greeks;
  if(riskResult.Failure is {Code:not "GreeksCalculationFailed"})greeks=new(){IsValid=false,FuturesContractId=contract.UnderlyingContractId,CalculatedAtUtc=at,FailureReason=OptionGreeksFailureReason.PricingContextUnavailable,PricingFailure=riskResult.Failure};
  else greeks=new(){IsValid=g is not null,PriceSource=OptionGreeksPriceSource.QuoteMidpoint,FuturesContractId=contract.UnderlyingContractId,FuturesPrice=6000.125m,OptionMarkPrice=10.25m,RiskFreeRate=context.Rate.AnnualContinuousRate,
   TimeToExpiryYears=g?.TimeToExpiry,ImpliedVolatility=g?.ImpliedVolatility,TheoreticalPrice=g?.TheoreticalPrice,Delta=g?.Delta,Gamma=g?.Gamma,Theta=g?.Theta,Vega=g?.Vega,Rho=g?.Rho,
   FuturesPriceSourceSequence=evidenceQuote.Sequence,OptionPriceSourceSequence=optionQuote.Sequence,FuturesPriceTimestamp=evidenceQuote.EventAtUtc,OptionPriceTimestamp=optionQuote.EventAtUtc,
   CalculatedAtUtc=at,PricingContextDigest=g?.ContextDigest,PricingFailure=riskResult.Failure};
  var captured=new QuoteCapture();var observationPublisher=new NativeOptionObservationPublisher(generation,captured,_=>true,new FixedTime(currentAt));
  var tick=new LastQuoteTickSnapshot(contract.ContractId,new(2026,10,27),10.25m,3,0,10.25m,3,0,77,at.AddTicks(-2),at.AddTicks(-1));
  var quoteEvent=new FuturesOptionChainQuoteChangedServiceEvent(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),contract.UnderlyingContractId,contract.ContractId,tick.ValueDate,DateOnly.FromDateTime(contract.ExpirationUtc.UtcDateTime),tick,greeks);
  await observationPublisher.PublishQuoteAsync(quoteEvent,context,mode!="success");
  var quoteGreeks=MessagePackSerializer.Serialize(new NativeQuoteGreeksResult(optionQuote,NativeQueryGreeks.FromSnapshot(greeks),new Guid(77,0,0,new byte[8])));
  risks.Add(new{quoteGreeks,snapshot=MessagePackSerializer.Serialize(captured.Value!.OptionMarketPriceObservation!.OptionTickData),quoteEvent=MessagePackSerializer.Serialize(captured.Value),mode,currentAt=currentAt.UtcTicks-DateTime.UnixEpoch.Ticks,content=MessagePackSerializer.Serialize(result.GetType(),result)});
 }
 var universes=new List<object>();
 foreach(var mode in new[]{"valid","incomplete","missing","version","digest","dataset","symbol","underlying","strike","right","no-expiry","precision","expired","unsupported","duplicate-contract","duplicate-instrument"}) {
  var definition=new TomasAI.IFM.Shared.MarketData.Definitions.OptionContractDefinition {Dataset=contract.Dataset,RawSymbol=contract.RawSymbol,Ticker="ES",Underlying=contract.UnderlyingContractId,
   Instrument=new(contract.PublisherId,contract.InstrumentId),Right=TomasAI.IFM.Shared.MarketData.Definitions.OptionRightSelection.Call,StrikePrice=6000m,MaturityDate=DateOnly.FromDateTime(contract.ExpirationUtc.UtcDateTime),
   ExpirationTimestampNanoseconds=checked((ulong)(contract.ExpirationUtc.UtcTicks-DateTime.UnixEpoch.Ticks)*100)};
  var mapping=mode=="missing"?null:mode=="unsupported"?contract with{SettlementStyle=(OptionSettlementStyle)99}:contract;
  var candidate=new OptionDefinitionCandidate(contract.ContractId,mode=="version"?"wrong":contract.MappingVersion,mode=="digest"?new string('0',64):contract.DefinitionDigest,
   mode switch {"dataset"=>definition with{Dataset="wrong"},"symbol"=>definition with{RawSymbol="wrong"},"underlying"=>definition with{Underlying="wrong"},"strike"=>definition with{StrikePrice=0},
    "right"=>definition with{Right=TomasAI.IFM.Shared.MarketData.Definitions.OptionRightSelection.Both},"no-expiry"=>definition with{ExpirationTimestampNanoseconds=null},
    "precision"=>definition with{ExpirationTimestampNanoseconds=definition.ExpirationTimestampNanoseconds+1},_=>definition});
  OptionDefinitionCandidate[] scope=mode=="duplicate-contract"?[candidate,candidate with{Definition=definition with{Instrument=new(3,43)}}]:mode=="duplicate-instrument"?[candidate,candidate with{ContractId="different"}]:[candidate];
  var when=mode=="expired"?contract.ExpirationUtc:at;
  var qualified=await new EuropeanOptionUniverse(new MappingFixture(mapping)).QualifyAsync(scope,mode!="incomplete",when,CancellationToken.None);
  universes.Add(new{mode,at=when.UtcTicks-DateTime.UnixEpoch.Ticks,complete=mode!="incomplete",candidates=scope.Select(c=>new{contract=c.ContractId,version=c.MappingVersion,digest=c.DefinitionDigest,
   dataset=c.Definition.Dataset,publisher=c.Definition.Instrument.PublisherId,instrument=c.Definition.Instrument.InstrumentId,symbol=c.Definition.RawSymbol,underlying=c.Definition.Underlying,
   right=(byte)c.Definition.Right,strike=decimal.GetBits(c.Definition.StrikePrice),expiration=c.Definition.ExpirationTimestampNanoseconds,mapping=mapping is null?null:MessagePackSerializer.Serialize(mapping)}),
   content=MessagePackSerializer.Serialize((qualified.Definitions,qualified.Exclusions,qualified.Failure))});
 }
 var compositions=new List<object>();
 foreach(var mode in new[]{"selection","missing","risk"}) {
  var iv=new ChainIvObservation(.2,context,evidenceQuote,optionQuote,at,"OptionRefresh/TestDefaults/v1");
  var selected=OptionSelectionValue.From(new ChainSelectionCalculation(context,evidenceQuote,optionQuote,iv,10.25,.3,at,null),5000);
  var item=new CompositionMarketInstrument(contract.ContractId,mode=="missing"?null:optionQuote,context,6000m,true,evidenceQuote)
   {Selection=mode=="selection"?selected:null,SessionVolume=100,OpenInterest=50,StatisticsAtUtc=at.AddSeconds(-1),VolumeValueDate=new(2026,10,27),OpenInterestValueDate=new(2026,10,26),
    QuoteUnavailableReason=mode=="missing"?"QuoteAgeExceeded":null,LastQuoteEventAtUtc=mode=="missing"?at.AddSeconds(-6):null,LastQuoteReceivedAtUtc=mode=="missing"?at.AddSeconds(-5):null};
  var composition=new MarketCompositionSnapshot(mode=="risk"?1:2,Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),"native-fixture","fixture-digest","Daily",generation,at,at.AddSeconds(1),[new(item,mode=="risk"?fullGreeks:null)],"");
  composition=composition with{Digest=PricingSemanticHash.Compute(composition)};
  var query=new CompositionSnapshotRequest(composition.SnapshotId,composition.ScopeId,"Daily",generation,at,at.AddSeconds(1),true)
   {SelectionOnly=mode!="risk",AllowMissingOptionQuotes=mode=="missing"};
  var capture=await new MarketCompositionSnapshotProvider(new CompositionSource(new("fixture-digest",generation,1,[item],null)),new FixedTime(at)).CaptureAsync(query,CancellationToken.None);
  byte[]? rankingPolicy=null,rankedContent=null;
  if(mode=="risk") {
   using var policyFixture=JsonDocument.Parse(await File.ReadAllTextAsync("TomasAI.IFM.Application.MarketData.Rust/qualification/ContextWireBridge/strategy-policy.json"));
   rankingPolicy=Convert.FromBase64String(policyFixture.RootElement[0].GetProperty("content").GetString()!);
   var rankedPolicy=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet>(rankingPolicy);
   var wingItem=item with{ContractId=item.ContractId+"-wing",Strike=item.Strike+5,IsCall=true};
   var rankSource=ImmutableArray.Create(new CompositionInstrumentSnapshot(item,fullGreeks),new CompositionInstrumentSnapshot(wingItem,fullGreeks with{Delta=.01}));
   var rankedRows=TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCandidatePreparation.Select(rankSource,rankedPolicy.BiasRows[0],at);
   if(rankedRows.Length!=2)throw new InvalidDataException("Ranked full-graph fixture did not select exact wing.");
   var rankedUntil=rankedRows.Min(x=>new[]{x.Instrument.Pricing!.ValidUntilUtc,x.Instrument.Quote!.EventAtUtc.AddMilliseconds(x.Instrument.Pricing.MaximumQuoteAgeMilliseconds),x.Instrument.Quote.ReceivedAtUtc.AddMilliseconds(x.Instrument.Pricing.MaximumQuoteAgeMilliseconds),x.Instrument.Underlying!.EventAtUtc.AddMilliseconds(x.Instrument.Pricing.MaximumQuoteAgeMilliseconds),x.Instrument.Underlying.ReceivedAtUtc.AddMilliseconds(x.Instrument.Pricing.MaximumQuoteAgeMilliseconds)}.Min());
   var rankedSnapshot=composition with{Instruments=rankedRows,ValidUntilUtc=rankedUntil,Digest="",StrategyOptionChainParameters=rankedPolicy,StrategyOptionChainBias="Neutral",StrategyOptionChainValueDate=new(2026,10,27)};
   rankedSnapshot=rankedSnapshot with{Digest=PricingSemanticHash.Compute(rankedSnapshot)};rankedContent=MessagePackSerializer.Serialize(rankedSnapshot);
  }
  compositions.Add(new{mode,rankingPolicy,rankedContent,content=MessagePackSerializer.Serialize(composition),digest=composition.Digest,
   capture=capture.Snapshot is null?null:MessagePackSerializer.Serialize(capture.Snapshot),captureFailure=capture.Failure?.Code});
 }
 rows.Add(new{universes,compositions,selections,risks,calculations,evidenceSuccess=MessagePackSerializer.Serialize(successful),evidenceFailure=MessagePackSerializer.Serialize(failed),evidenceMissing=MessagePackSerializer.Serialize(missing),quoteDigest=PricingSemanticHash.Compute(evidenceQuote),raw=JsonSerializer.SerializeToElement(payload),name=$"{schema}/{exercise}/{fraction}",at=at.UtcTicks-DateTime.UnixEpoch.Ticks,until=validUntil.UtcTicks-DateTime.UnixEpoch.Ticks,
 contract=MessagePackSerializer.Serialize(contract),rollover=calendar.ValueDateRollover.Ticks,context=MessagePackSerializer.Serialize(context),digest=PricingSemanticHash.Compute(context),
 manifestContent=MessagePackSerializer.Serialize(new WorkerOptionDefinition(context,6000m,true))});
}
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {rows.Count} exact C# context wire and semantic fixtures.");

if (args.Length > 1) {
 var sources = new List<object>();
 foreach (var price in new[]{0m, .000000001m, -1.2300m, 12345.678900m, decimal.MaxValue})
 foreach (var revision in new[]{0,1}) {
  var tradeSource = new OptionTradeSource("GLBX.MDP3",ushort.MaxValue,uint.MaxValue,"ES<\"&\\\u6f22\u5b57",new(2026,10,27),price,uint.MaxValue,long.MaxValue,
   1793102400123456789,1793102400123456999+revision,revision==0?generation:Guid.Empty,"ES option \u0001");
  sources.Add(new{price=decimal.GetBits(price),revision,wire=MessagePackSerializer.Serialize(tradeSource),identity=tradeSource.Identity,sourceDigest=tradeSource.SourceDigest,
   semantic=PricingSemanticHash.Compute(tradeSource)});
 }
 await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(sources,new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine($"PASS exported {sources.Count} exact C# source wire and identity fixtures.");
}

sealed class EvidenceCapture:IOptionTradeEvidenceWriter {
 public OptionTradeEvidence? Value{get;private set;}
 public ValueTask WriteAsync(OptionTradeEvidence evidence,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();Value=evidence;return ValueTask.CompletedTask;}
}

sealed class FixedTime(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
sealed class QuoteCapture:ITickAggregationEventPublisher {
 public FuturesTickTradeDataChangedEvent? Value;
 public bool IsRunning=>true;
 public ValueTask StartAsync()=>ValueTask.CompletedTask;public ValueTask StopAsync()=>ValueTask.CompletedTask;public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
 public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value)=>ValueTask.CompletedTask;
 public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value){Value=value;return ValueTask.CompletedTask;}
 public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease)=>ValueTask.CompletedTask;
}

sealed class CompositionSource(CompositionMarketPage page):ICompositionMarketSource {
 public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request,string? continuation,CancellationToken cancellationToken) {
  cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(page);
 }
}

sealed class MappingFixture(OptionPricingConvention? mapping):IOptionPricingConventionStore {
 public Task<OptionPricingConvention?> GetAsync(string contractId,string mappingVersion,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(mapping);}
}
