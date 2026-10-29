using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Pricing;
var at=new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);var generation=Guid.Parse("29a8cd30-5aeb-4b87-bfdf-b12cfd8b9dd9");
var conversion=new TreasuryRateConversionPolicy("FinancialModelingPrep","fixture:CMT",TreasuryRateConvention.UsTreasuryCmtNominalSemiannual,"test/v1","synthetic-evidence");
var curve=new TreasuryCurveSnapshot(new(2026,9,8),[new(TreasuryTenor.OneMonth,5m)],at,"FinancialModelingPrep");
var calendar=new OptionPricingCalendar("fixture-calendar/v1","America/New_York",new(2026,1,1),new(2026,12,31),new(18,0),Enumerable.Range(0,365).Select(i=>new DateOnly(2026,1,1).AddDays(i)).Where(d=>d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
OptionPricingContext Context(bool american,bool call,bool futuresStyle){
 var contract=new OptionPricingConvention{SchemaVersion=3,ContractId="ES-option",Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=10,RawSymbol="fixture-option",Root=futuresStyle?"SYNTHETIC":"ES",Exchange="XCME",Currency="USD",UnderlyingContractId="ES-future",
 ExerciseStyle=american?OptionExerciseStyle.American:OptionExerciseStyle.European,SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=at.AddDays(24),LastTradingUtc=at.AddDays(24),DayCount=PricingDayCount.Actual365Fixed,CalendarVersion=calendar.Version,Multiplier=50,TickSize=.25m,TickRuleVersion="fixture-tick/v1",DefinitionDigest=new('a',64),MappingVersion="fixture-mapping/v1",EvidenceId="synthetic-series",EffectiveFromUtc=at.AddDays(-10),EffectiveUntilUtc=at.AddDays(40),PremiumStyle=futuresStyle?OptionPremiumStyle.FuturesStyle:OptionPremiumStyle.PaidUpfront,UnderlyingKind=PricingUnderlyingKind.Futures,Strike=5000,Right=call?PricingOptionRight.Call:PricingOptionRight.Put,PremiumTickRule=OptionPremiumTickRule.Fixed};
 return new(contract,calendar,TreasuryRateConversion.Convert(curve,TreasuryTenor.OneMonth,conversion).Value!,at.AddHours(1),generation,Black76PricingModel.EngineFor(contract),1000,250,"fixture-publication/v1");
}
OptionPricingQuote Quote(string id,decimal mid)=>new(id,mid-.25m,mid+.25m,10,10,at,at,1,generation);
await using var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]));var bridge=new RustOptionPricing(runtime);int count=0;
async Task Compare(OptionPricingContext context,OptionPricingQuote underlying,OptionPricingQuote option,bool call){
 var expected=Black76PricingModel.Calculate(context,underlying,option,5000,call,at);
 var actual=await bridge.CalculateAsync(context,underlying,option,5000,call,at,TimeSpan.FromMinutes(2));
 var a=MessagePackBinarySerializer.Shared.Serialize(actual)!;var b=MessagePackBinarySerializer.Shared.Serialize(expected)!;
 if(!a.SequenceEqual(b)) throw new InvalidDataException($"Application pricing mismatch case {count}; expected {expected}, actual {actual}");count++;
}
foreach(var american in new[]{false,true})foreach(var call in new[]{false,true})foreach(var futuresStyle in new[]{false,true}){
 var context=Context(american,call,futuresStyle);var underlying=Quote("ES-future",5000);var option=Quote("ES-option",100);
 await Compare(context,underlying,option,call);
 foreach(var changed in new[]{option with{GenerationId=Guid.NewGuid()},option with{Bid=0},option with{BidSize=-1},option with{Sequence=-1},option with{EventAtUtc=at.AddMilliseconds(-1001)},option with{ReceivedAtUtc=at.AddTicks(1)},option with{EventAtUtc=at.AddMilliseconds(1)},option with{EventAtUtc=at.AddMilliseconds(-251)},option with{ContractId="OTHER"},option with{Ask=.01m}})await Compare(context,underlying,changed,call);
 await Compare(context with{GenerationId=Guid.Empty},underlying,option,call);await Compare(context with{ValidUntilUtc=at},underlying,option,call);
}
Console.WriteLine($"Application/native pricing: {count} exact MessagePack values/failures/context digests matched unchanged C#.");

// Differentially test the native hot validator without invoking a numerical
// solver for every mutation. The expected result is the unchanged application
// validator, not a second copy of its conditions.
var random = new Random(727261);
var qualified = Context(false, true, false);
var baseUnderlying = Quote("ES-future", 5000); var baseOption = Quote("ES-option", 100);
decimal Money() => new(unchecked((int)random.NextInt64(0, 1L << 32)), unchecked((int)random.NextInt64(0, 1L << 32)),
    unchecked((int)random.NextInt64(0, 1L << 32)), false, (byte)random.Next(29));
for (var index = 0; index < 10_000; index++)
{
    var underlying = baseUnderlying; var option = baseOption; decimal strike = 5000;
    switch (index % 10)
    {
        case 0: var ask = Money(); option = option with { Bid = Money(), Ask = index % 20 == 0 ? -ask : ask }; break;
        case 1: var bid = Money(); option = option with { Bid = bid, Ask = bid, BidSize = Money(), AskSize = Money() }; break;
        case 2: option = option with { BidSize = -.0000000000000000000000000001m }; break;
        case 3: option = option with { EventAtUtc = at.AddTicks(-10_000_000 + random.Next(-2, 3)) }; break;
        case 4: underlying = underlying with { ReceivedAtUtc = at.AddTicks(-10_000_000 + random.Next(-2, 3)) }; break;
        case 5: option = option with { EventAtUtc = at.AddTicks(-2_500_000 + random.Next(-2, 3)) }; break;
        case 6: option = option with { GenerationId = Guid.Empty }; break;
        case 7: option = option with { EventAtUtc = at.ToOffset(TimeSpan.FromHours(1)) }; break;
        case 8: strike = index % 20 == 8 ? 0 : decimal.MaxValue; break;
        case 9: option = option with { Bid = new decimal(0, 0, 0, true, 28), AskSize = decimal.MaxValue }; break;
    }
    var expected = Black76PricingModel.ValidateInputs(qualified, underlying, option, strike, true, at);
    var actual = runtime.ValidateOptionInputs(qualified, underlying, option, strike, true, at);
    if (expected != actual) throw new InvalidDataException($"Native quote qualification parity case {index}: expected {expected}, actual {actual}.");
}
Console.WriteLine("Native quote qualification: 10,000 exact C# differential cases passed, including 96-bit decimals, 28-digit scales, signed zero, fractional quantities, one-tick age/skew boundaries and failure ordering.");

var trades=new RustOptionTradePricing(runtime);var tradeCases=0;
foreach(var american in new[]{false,true})foreach(var call in new[]{false,true})foreach(var futuresStyle in new[]{false,true}){
    var context=Context(american,call,futuresStyle);
    for(var variant=0;variant<10;variant++){
        var underlying=Quote("ES-future",5000m);OptionPricingQuote? source=underlying;
        var tick=new LastTradeTickSnapshot("ES-option",new(2026,9,8),100m,2,99,at,at);
        tick=variant switch{
            1=>tick with{Size=0},2=>tick with{ValueDate=default},3=>tick with{Price=-1},4=>tick with{Price=10000},
            5=>tick with{EventTimestamp=at.AddSeconds(-2)},6=>tick with{ReceiveTimestamp=at.AddTicks(1)},
            7=>tick with{ContractId="another-contract"},8=>tick with{SourceSequence=-1},_=>tick};
        if(variant==9)source=null;
        var expected=OptionTradePricing.Calculate(context,source,tick,5000,call,at);
        var actual=await trades.CalculateAsync(context,source,tick,5000,call,at,TimeSpan.FromMinutes(2));
        if(!MessagePackBinarySerializer.Shared.Serialize(expected)!.SequenceEqual(MessagePackBinarySerializer.Shared.Serialize(actual)!))
            throw new InvalidDataException($"Native trade pricing parity case {tradeCases}: expected {expected}; actual {actual}.");
        tradeCases++;
    }
}
var evidenceContext=Context(false,true,false);var evidenceUnderlying=Quote("ES-future",5000m);
var eventNs=checked((at.UtcTicks-DateTimeOffset.UnixEpoch.Ticks)*100)+37;
var sourceTrade=new OptionTradeSource(evidenceContext.Contract.Dataset,evidenceContext.Contract.PublisherId,evidenceContext.Contract.InstrumentId,
    evidenceContext.Contract.ContractId,new(2026,9,8),100,2,99,eventNs,eventNs+46,generation,evidenceContext.Contract.RawSymbol);
var writer=new CapturedEvidence();
var retained=await trades.CalculateAndRetainAsync(sourceTrade,"ES-future",evidenceContext,evidenceUnderlying,5000,true,at,writer,TimeSpan.FromSeconds(2));
var retainedEvidence=writer.Value??throw new InvalidDataException("Trade result returned without acknowledged evidence.");
retainedEvidence.Validate();if(retainedEvidence.Source!=sourceTrade||retainedEvidence.Greeks?.ContextDigest!=retained.PricingContextDigest)
    throw new InvalidDataException("Trade evidence lost native nanoseconds or first-attempt provenance.");
var missingContext=new CapturedEvidence();var unavailable=await trades.CalculateAndRetainAsync(sourceTrade,"ES-future",null,evidenceUnderlying,5000,true,at,missingContext,TimeSpan.FromSeconds(2));
if(unavailable.IsValid||missingContext.Value?.Context is not null||missingContext.Value?.Underlying is not null||missingContext.Value?.Failure?.Code!="PricingContextUnavailable")
    throw new InvalidDataException("Missing-context trade was not retained as an explicit failed attempt.");
try{await trades.CalculateAndRetainAsync(sourceTrade,"ES-future",evidenceContext,evidenceUnderlying,5000,true,at,new FailedEvidence(),TimeSpan.FromSeconds(2));
    throw new InvalidDataException("Unacknowledged evidence write was reported successful.");}catch(IOException){}
var stalled=new StalledEvidence();var elapsed=System.Diagnostics.Stopwatch.StartNew();
try{await trades.CalculateAndRetainAsync(sourceTrade,"ES-future",evidenceContext,evidenceUnderlying,5000,true,at,stalled,TimeSpan.FromMilliseconds(100));
    throw new InvalidDataException("Stalled evidence writer was reported successful.");}catch(OperationCanceledException){}
if(!stalled.Called||elapsed.Elapsed>TimeSpan.FromSeconds(2))throw new InvalidDataException("Evidence acknowledgement escaped its original caller budget.");
Console.WriteLine($"Native actual-trade pricing: {tradeCases} exact C# full DTO/digest cases; independent IV, exact nanosecond evidence, missing-context retention and explicit writer failure/deadline passed.");

sealed class CapturedEvidence:IOptionTradeEvidenceWriter{
    internal OptionTradeEvidence? Value;
    public ValueTask WriteAsync(OptionTradeEvidence evidence,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();evidence.Validate();Value=evidence;return ValueTask.CompletedTask;}
}
sealed class FailedEvidence:IOptionTradeEvidenceWriter{
    public ValueTask WriteAsync(OptionTradeEvidence evidence,CancellationToken cancellationToken)=>ValueTask.FromException(new IOException("Injected evidence persistence failure"));
}
sealed class StalledEvidence:IOptionTradeEvidenceWriter{
    internal bool Called;
    public ValueTask WriteAsync(OptionTradeEvidence evidence,CancellationToken cancellationToken){Called=true;return new(Task.Delay(Timeout.Infinite));}
}
