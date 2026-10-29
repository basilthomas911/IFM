using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Definitions;
var library=Path.GetFullPath(args[0]);var wire=await File.ReadAllBytesAsync(args[1]);
var baseAt=new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);var at=baseAt.AddSeconds(1);var budget=TimeSpan.FromSeconds(5);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();int accepted=0,closed=0;
var server=Task.Run(async()=>{try{while(true){var client=await listener.AcceptTcpClientAsync(stopping.Token);Interlocked.Increment(ref accepted);
    peers.Add(Task.Run(async()=>{using(client){try{var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
        await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);
        if(await reader.ReadLineAsync(stopping.Token)is null)return;await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);
        string? line;int count=0;while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session")count++;
        if(line is null)return;if(count!=2)throw new InvalidDataException("Expected trades and quote subscriptions.");
        await stream.WriteAsync(wire,stopping.Token);if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected provider cleanup EOF.");
    }catch(IOException){}finally{Interlocked.Increment(ref closed);}}}));
}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
await using var runtime=new NativeMarketDataRuntime(library,2);var cache=new NativeOptionPricingCache(runtime);
runtime.ConfigureLive(new(2026,9,8),Guid.NewGuid(),at,false,64);await runtime.PrepareOptionOwnerAsync(Guid.Empty,budget);
var generation=runtime.ReadOptionOwnerGeneration();
var calendar=new OptionPricingCalendar("fixture-calendar/v1","America/New_York",new(2026,1,1),new(2026,12,31),new(18,0),
    Enumerable.Range(0,365).Select(i=>new DateOnly(2026,1,1).AddDays(i)).Where(d=>d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
var conversion=new TreasuryRateConversionPolicy("FinancialModelingPrep","fixture:CMT",TreasuryRateConvention.UsTreasuryCmtNominalSemiannual,"test/v1","synthetic-evidence");
var curve=new TreasuryCurveSnapshot(new(2026,9,8),[new(TreasuryTenor.OneMonth,5m)],baseAt,"FinancialModelingPrep");
var contract=new OptionPricingConvention{SchemaVersion=3,ContractId="ES20261002C6000",Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=43,
    RawSymbol="fixture-option",Root="ES",Exchange="XCME",Currency="USD",UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,
    SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=baseAt.AddDays(24),LastTradingUtc=baseAt.AddDays(24),DayCount=PricingDayCount.Actual365Fixed,
    CalendarVersion=calendar.Version,Multiplier=50,TickSize=.25m,TickRuleVersion="fixture-tick/v1",DefinitionDigest=new('a',64),MappingVersion="fixture-mapping/v1",
    EvidenceId="synthetic-series",EffectiveFromUtc=baseAt.AddDays(-10),EffectiveUntilUtc=baseAt.AddDays(40),PremiumStyle=OptionPremiumStyle.PaidUpfront,
    UnderlyingKind=PricingUnderlyingKind.Futures,Strike=6000,Right=PricingOptionRight.Call,PremiumTickRule=OptionPremiumTickRule.Fixed};
var context=new OptionPricingContext(contract,calendar,TreasuryRateConversion.Convert(curve,TreasuryTenor.OneMonth,conversion).Value!,baseAt.AddHours(1),
    generation,Black76PricingModel.EngineFor(contract),1000,250,"fixture-publication/v1");
var prepared=new PreparedOptionPricingContext(Guid.NewGuid(),context,42,6000,true,true,at.AddMinutes(1));
var policy=new OptionPricingRefreshPolicy{PriceDeltaMilliseconds=10,FullRiskMilliseconds=20,ImpliedVolatilityMilliseconds=5000};
NativeSubscription[] subscriptions=[new(["42","43"],"trades","instrument_id"),new(["42","43"],"mbp-1","instrument_id")];
NativeRegistration[] registrations=[new(42,contract.UnderlyingContractId,"42",false,null,false,true,3),new(43,contract.ContractId,"43",true,null,false,true,3)];
var underlying=new OptionPricingQuote(contract.UnderlyingContractId,6000m,6000.25m,123,456,baseAt.AddTicks(16),at,9,generation);
var option=new OptionPricingQuote(contract.ContractId,99.75m,100.25m,10,10,baseAt.AddTicks(18),at,10,generation);
var route=new DatabentoOptionChainRoute{FuturesOptionContractId=contract.ContractId,Definition=new(){Dataset=contract.Dataset,RawSymbol=contract.RawSymbol,
    Ticker=contract.Root,Underlying=contract.UnderlyingContractId,Instrument=new(contract.PublisherId,contract.InstrumentId),Right=OptionRightSelection.Call,
    StrikePrice=6000,MaturityDate=new(2026,10,2),ExpirationTimestampNanoseconds=checked((ulong)(contract.ExpirationUtc.UtcTicks-DateTimeOffset.UnixEpoch.Ticks)*100)}};
var inputs=new OptionChainPricingInputStore();inputs.Set(new(context,underlying));
var currentQuote=new TaskCompletionSource<FuturesOptionChainQuoteChangedServiceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
await using var current=new CoalescedOptionChainPricing(inputs,generation,policy,_=>underlying,new FixedTime(at),publishPricing:value=>{if(value.Greeks.IsValid)currentQuote.TrySetResult(value);return ValueTask.CompletedTask;});current.Register(route);current.RequireRisk(contract.ContractId,true);
current.EnrichQuote(route,new(contract.ContractId,new(2026,9,8),99.75m,10,7,100.25m,10,8,10,baseAt.AddTicks(18),baseAt.AddTicks(19)){LocalReceivedAtUtc=at});
void Equal<T>(T expected,T actual,string stage){
    var a=MessagePackBinarySerializer.Shared.Serialize(expected)!;var b=MessagePackBinarySerializer.Shared.Serialize(actual)!;
    if(!a.SequenceEqual(b))throw new InvalidDataException($"{stage} differs from current C# result. Expected={expected}; Actual={actual}");
}
async Task< OptionPricingPassResult> WaitRisk(PreparedOptionPricingContext entry){
    using var timeout=new CancellationTokenSource(budget);
    while(true){var risk=cache.ReadRisk("priced",generation,entry,at);if(risk?.Value is not null)return risk;
        await runtime.WaitForLiveChangesAsync(timeout.Token);
    }
}
try{
    var saved=new OptionChainStatisticsSnapshot(contract.ContractId,12345,new(2026,9,8),222,new(2026,9,7));
    await runtime.StartPricedOptionScopeAsync("priced",new('a',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,registrations,true,
        new('b',64),[prepared],policy,false,at,budget,statistics:[saved,new(contract.ContractId,99999,new(2026,9,9),99999,new(2026,9,9)),
            new("unknown-contract",100,new(2026,9,8),100,new(2026,9,8))]);
    if(!runtime.ReadOptionScopeState("priced",43,3).TryGetOptionStatistics(contract.ContractId,out var seeded))throw new InvalidDataException("Saved option statistics missing from native state.");
    Equal(saved,seeded,"saved statistics / independent reference dates");
    Equal(Black76PricingModel.Calculate(context,underlying,option,6000,true,at),await WaitRisk(prepared),"full risk / semantic digest");
    current.ProcessPass();current.ProcessPass(true);
    var published=await currentQuote.Task.WaitAsync(budget);
    var nativeQuote=cache.ReadQuoteGreeks("priced",generation,prepared,at) ?? throw new InvalidDataException("Native quote Greeks missing.");
    Equal(published.Greeks,nativeQuote.Greeks,"enriched quote Greeks / exact frozen inputs");
    Equal(option,nativeQuote.Quote,"enriched quote original source");
    var originalEvents=new CapturedTickPublisher();var nativeEvents=new CapturedTickPublisher();
    var originalPublisher=new IndividualOptionObservationPublisher(generation,inputs,originalEvents,_=>true,new FixedTime(at));
    var nativePublisher=new NativeOptionObservationPublisher(generation,nativeEvents,_=>true,new FixedTime(at));
    var sourceQuote=published with{EventId=Guid.NewGuid()};
    await originalPublisher.PublishAsync(sourceQuote);await nativePublisher.PublishQuoteAsync(sourceQuote with{Greeks=nativeQuote.Greeks},context,false);
    Equal(originalEvents.Events[^1],nativeEvents.Events[^1],"complete actor quote event / current C# envelope and financial fields");
    sourceQuote=sourceQuote with{EventId=Guid.NewGuid()};
    await originalPublisher.PublishPricingAsync(sourceQuote);await nativePublisher.PublishQuoteAsync(sourceQuote with{Greeks=nativeQuote.Greeks},context,true);
    Equal(originalEvents.Events[^1],nativeEvents.Events[^1],"complete actor risk completion / current C# revision");
    await originalPublisher.PublishAsync(sourceQuote);await nativePublisher.PublishQuoteAsync(sourceQuote,context,false);
    if(originalEvents.Events.Count!=2||nativeEvents.Events.Count!=2)throw new InvalidDataException("Unchanged quote suppression differs.");
    var pendingQuote=sourceQuote with{EventId=Guid.NewGuid(),Greeks=new(){FuturesContractId=contract.UnderlyingContractId,CalculatedAtUtc=at,
        FailureReason=OptionGreeksFailureReason.PricingContextUnavailable,PricingFailure=new("CalculationPending","OptionPricing",contract.ContractId,"Qualified calculation is unavailable.")}};
    await originalPublisher.PublishPricingAsync(pendingQuote);await nativePublisher.PublishQuoteAsync(pendingQuote,context,true);
    Equal(originalEvents.Events[^1],nativeEvents.Events[^1],"unavailable Greeks / actor event null and zero policy");
    using(var cancelledPublication=new CancellationTokenSource()){
        cancelledPublication.Cancel();try{await nativePublisher.PublishQuoteAsync(sourceQuote,context,true,cancelledPublication.Token);throw new InvalidDataException("Cancelled observation published.");}catch(OperationCanceledException){}
    }
    if(nativeEvents.Events.Count!=3)throw new InvalidDataException("Cancelled observation changed event revision.");
    var previousRevision=nativeEvents.Events[^1].OptionMarketPriceObservation!.OptionTickData.ObservationRevision;
    nativePublisher.RetainContracts([]);await nativePublisher.PublishQuoteAsync(sourceQuote,context,false);
    if(nativeEvents.Events[^1].OptionMarketPriceObservation!.OptionTickData.ObservationRevision<=previousRevision)throw new InvalidDataException("Monitoring prune reset generation-wide observation revision.");
    var readerPreparation=prepared;
    var reader=new NativeOptionPriceReader(runtime,"priced",generation,new(2026,9,8),()=>Volatile.Read(ref readerPreparation),new FixedTime(at));
    if(!reader.TryGetLastQuote(out var rawQuote))throw new InvalidDataException("Native option raw quote unavailable.");
    Equal(published.Tick,rawQuote,"stable native raw quote / original C# tick");
    if(!reader.TryGetLastQuoteWithGreeks(out var enrichedQuote))throw new InvalidDataException("Native enriched quote unavailable.");
    Equal(new LastQuoteTickWithGreeksSnapshot(published.Tick,published.Greeks),enrichedQuote,"stable native enriched quote / atomic source");
    if(!reader.TryGetLastTrade(out var rawTrade)||reader.TryGetLastTradeWithGreeks(out _))throw new InvalidDataException("Raw trade/enrichment acknowledgment distinction failed.");
    var tradePricing=new RustOptionTradePricing(runtime);
    var writer=new CapturedEvidence();
    var source=new OptionTradeSource(contract.Dataset,contract.PublisherId,contract.InstrumentId,contract.ContractId,new(2026,9,8),100m,5,11,
        checked((baseAt.UtcTicks-DateTimeOffset.UnixEpoch.Ticks)*100+2000),checked((baseAt.UtcTicks-DateTimeOffset.UnixEpoch.Ticks)*100+2100),generation,contract.RawSymbol);
    var tradeGreeks=await tradePricing.CalculateAndRetainAsync(source,contract.UnderlyingContractId,context,underlying,6000,true,at,writer,budget);
    Equal(OptionTradePricing.Calculate(context,underlying,rawTrade,6000,true,at),tradeGreeks,"SDK actual-trade IV / original C# Greeks");
    if(writer.Value?.Source!=source)throw new InvalidDataException("SDK trade evidence source was not acknowledged.");
    var enrichedTrade=new LastTradeTickWithGreeksSnapshot(rawTrade,tradeGreeks);
    if(reader.RetainAcknowledgedTrade(Guid.NewGuid(),prepared.Identity,enrichedTrade)||reader.RetainAcknowledgedTrade(nativeQuote.PhysicalGeneration,Guid.NewGuid(),enrichedTrade))throw new InvalidDataException("Foreign trade source/context accepted.");
    if(!reader.RetainAcknowledgedTrade(nativeQuote.PhysicalGeneration,prepared.Identity,enrichedTrade)||!reader.TryGetLastTradeWithGreeks(out var retainedTrade))throw new InvalidDataException("Acknowledged current trade unavailable.");
    Equal(enrichedTrade,retainedTrade,"acknowledged exact trade reader");
    if(nativeQuote.PhysicalGeneration==Guid.Empty)throw new InvalidDataException("Native quote lacks physical generation fence.");
    using(var timeout=new CancellationTokenSource(budget)){
        while(cache.ReadSelection("priced",generation,prepared,policy.Version,at)?.Iv is null)await runtime.WaitForLiveChangesAsync(timeout.Token);
    }
    Equal(current.ReadSelection(contract.ContractId),cache.ReadSelection("priced",generation,prepared,policy.Version,at),"selection / retained IV provenance");
    if(cache.ReadSelection("priced",generation,prepared,"another policy version",at)is not null)throw new InvalidDataException("Reader relabelled IV with an incorrect refresh policy.");
    var compositionSource=new NativeOptionCompositionSource(runtime,"priced",new('e',64),generation,[prepared],new FixedTime(at),policy,
        ()=>Volatile.Read(ref readerPreparation).Identity==prepared.Identity);
    var nativeComposition=new NativeMarketCompositionSnapshotProvider(compositionSource,runtime,new FixedTime(at));
    foreach(var allowMissing in new[]{false,true})foreach(var selectionOnly in new[]{false,true})foreach(var selectedRisk in new[]{false,true}){
        var capture=new CompositionSnapshotRequest(Guid.NewGuid(),"priced","Daily",generation,at,at.AddSeconds(3),true)
            {AllowMissingOptionQuotes=allowMissing,SelectionOnly=selectionOnly,RiskContractIds=selectedRisk?[contract.ContractId]:[]};
        var expectedItem=new CompositionMarketInstrument(contract.ContractId,option,context,6000,true,underlying)
            {SessionVolume=saved.Volume,OpenInterest=saved.OpenInterest,VolumeValueDate=saved.VolumeValueDate,OpenInterestValueDate=saved.OpenInterestValueDate};
        if(selectionOnly&&!selectedRisk)expectedItem=expectedItem with{Selection=OptionSelectionValue.From(current.ReadSelection(contract.ContractId)!,policy.ImpliedVolatilityMilliseconds)};
        var expectedPage=new CompositionMarketPage(new('e',64),generation,1,[expectedItem],null);
        Equal(expectedPage,await compositionSource.ReadAsync(capture,null,default),"native composition source / complete current fields");
        var expectedCapture=await new MarketCompositionSnapshotProvider(new FrozenPage(expectedPage),new FixedTime(at)).CaptureAsync(capture,default);
        var actualCapture=await nativeComposition.CaptureAsync(capture,default);
        Equal(expectedCapture,actualCapture,"complete composition / exact digest and selection-risk policy");
        if(actualCapture.Snapshot is null&&!(selectionOnly&&selectedRisk&&!allowMissing))throw new InvalidDataException("Happy composition capture failed: "+actualCapture.Failure);
    }
    var validCapture=new CompositionSnapshotRequest(Guid.NewGuid(),"priced","Daily",generation,at,at.AddSeconds(3),true);
    foreach(var invalidCapture in new[]{validCapture with{SnapshotId=Guid.Empty},validCapture with{ScopeId=""},validCapture with{GenerationId=Guid.Empty},
        validCapture with{Horizon="Unknown"},validCapture with{MaximumContracts=0},validCapture with{MaximumContracts=2049},
        validCapture with{MaximumQuoteAgeMilliseconds=0},validCapture with{MaximumQuoteSkewMilliseconds=2001},
        validCapture with{EvaluatedAtUtc=at.AddTicks(1)},validCapture with{DeadlineUtc=at}}){
        var expectedInvalid=await new MarketCompositionSnapshotProvider(compositionSource,new FixedTime(at)).CaptureAsync(invalidCapture,default);
        Equal(expectedInvalid,await nativeComposition.CaptureAsync(invalidCapture,default),"invalid capture policy / current C# failure");
        if(expectedInvalid.Failure?.Code!="InvalidSnapshotRequest")throw new InvalidDataException("Invalid capture test did not reach the validation gate.");
    }
    var itemForFailure=new CompositionMarketInstrument(contract.ContractId,option,context,6000,true,underlying);
    foreach(var badPage in new[]{new CompositionMarketPage("scope",Guid.NewGuid(),1,[itemForFailure],null),
        new CompositionMarketPage("scope",generation,2,[itemForFailure],null),new CompositionMarketPage("scope",generation,1,[itemForFailure with{Quote=null}],null),
        new CompositionMarketPage("scope",generation,1,[itemForFailure with{Underlying=null}],null),
        new CompositionMarketPage("scope",generation,1,[itemForFailure with{Quote=option with{Bid=-1}}],null),
        new CompositionMarketPage("scope",generation,1,[itemForFailure with{Quote=option with{GenerationId=Guid.NewGuid()}}],null),
        new CompositionMarketPage("scope",generation,1,[itemForFailure with{Quote=option with{ReceivedAtUtc=at.AddTicks(1)}}],null),
        new CompositionMarketPage("scope",generation,1,[itemForFailure with{Quote=option with{EventAtUtc=at.AddSeconds(-10)}}],null)}){
        var sourceForFailure=new FrozenPage(badPage);
        var expectedFailure=await new MarketCompositionSnapshotProvider(sourceForFailure,new FixedTime(at)).CaptureAsync(validCapture,default);
        var actualFailure=await new NativeMarketCompositionSnapshotProvider(sourceForFailure,runtime,new FixedTime(at)).CaptureAsync(validCapture,default);
        Equal(expectedFailure,actualFailure,"incomplete/invalid quote source / current C# failure");
        if(expectedFailure.Failure is null)throw new InvalidDataException("Invalid source test unexpectedly succeeded.");
    }
    var shortCapture=validCapture with{DeadlineUtc=at.AddMilliseconds(100)};
    Equal(await new MarketCompositionSnapshotProvider(new StalledPage(),new FixedTime(at)).CaptureAsync(shortCapture,default),
        await new NativeMarketCompositionSnapshotProvider(new StalledPage(),runtime,new FixedTime(at)).CaptureAsync(shortCapture,default),"stalled source original capture timeout");
    using(var cancelled=new CancellationTokenSource()){
        cancelled.Cancel();try{await compositionSource.ReadAsync(new(Guid.NewGuid(),"priced","Daily",generation,at,at.AddSeconds(1),true),null,cancelled.Token);throw new InvalidDataException("Cancelled native source read succeeded.");}catch(OperationCanceledException){}
    }
    var demandConnections=Volatile.Read(ref accepted);
    await runtime.SetOptionScopeRiskRequirementsAsync("priced",generation,new('b',64),[new(43,prepared.Identity,false)],budget);
    await runtime.SetOptionScopeRiskRequirementsAsync("priced",generation,new('b',64),[new(43,prepared.Identity,true)],budget);
    try{await runtime.SetOptionScopeRiskRequirementsAsync("priced",generation,new('b',64),[new(43,Guid.NewGuid(),false)],budget);throw new InvalidDataException("Stale risk context was accepted.");}catch(InvalidOperationException){}
    try{await runtime.SetOptionScopeRiskRequirementsAsync("priced",generation,new('d',64),[new(43,prepared.Identity,false)],budget);throw new InvalidDataException("Foreign pricing digest changed business risk.");}catch(InvalidOperationException){}
    if(Volatile.Read(ref accepted)!=demandConnections)throw new InvalidDataException("Business risk control reconnected provider transport.");
    var before=Volatile.Read(ref accepted);
    await runtime.StartPricedOptionScopeAsync("priced",new('a',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,registrations,true,
        new('b',64),[prepared],policy,false,at,budget);
    if(Volatile.Read(ref accepted)!=before)throw new InvalidDataException("Identical priced acquire reconnected.");
    var nextContext=context with{Rate=TreasuryRateConversion.Convert(curve with{Rates=[new(TreasuryTenor.OneMonth,5.25m)]},TreasuryTenor.OneMonth,conversion).Value!};
    try{await runtime.StartPricedOptionScopeAsync("priced",new('a',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,registrations,true,
        new('b',64),[prepared with{Context=nextContext}],policy,false,at,budget);throw new InvalidDataException("Reused pricing digest hid changed native model inputs.");}catch(InvalidOperationException){}
    var next=prepared with{Identity=Guid.NewGuid(),Context=nextContext};
    await runtime.RefreshOptionScopePricingAsync("priced",generation,new('b',64),new('c',64),[new(prepared.Identity,next)],at,budget);
    Volatile.Write(ref readerPreparation,next);
    try{await runtime.SetOptionScopeRiskRequirementsAsync("priced",generation,new('b',64),[new(43,next.Identity,true)],budget);throw new InvalidDataException("Pre-refresh digest changed replacement risk.");}catch(InvalidOperationException){}
    await runtime.SetOptionScopeRiskRequirementsAsync("priced",generation,new('c',64),[new(43,next.Identity,true)],budget);
    try{await compositionSource.ReadAsync(new(Guid.NewGuid(),"priced","Daily",generation,at,at.AddSeconds(1),true),null,default);throw new InvalidDataException("Retired composition context was reused.");}catch(CompositionMarketSourceException error)when(error.Code=="Recovering"){}
    Equal(Black76PricingModel.Calculate(nextContext,underlying,option,6000,true,at),await WaitRisk(next),"atomic reference refresh risk / digest");
    if(cache.ReadRisk("priced",generation,prepared,at)is not null)throw new InvalidDataException("Old context reader returned replacement risk.");
    if(Volatile.Read(ref accepted)!=before)throw new InvalidDataException("Context refresh reconnected the provider.");
    using(var sourceReader=new NativeOptionTradeSourceReader(runtime,generation,new(2026,9,8))){
        var captured=new NativeCapturedOptionTrade?[16];var received=0;var consumed=0;
        do{
            var length=sourceReader.Drain("priced",captured,out consumed);
            for(var i=0;i<length;i++){
                var original=captured[i]!;received++;
                Equal(source,original.Source,"DLL source nanoseconds / queued trade identity");
                Equal(new WorkerOptionDefinition(context,6000,true),original.Definition,"queued original reference survives refresh");
                Equal(underlying,original.Underlying,"queued original underlying survives refresh");
                if(original.ContextIdentity!=prepared.Identity||original.PhysicalGeneration!=nativeQuote.PhysicalGeneration||original.CalculatedAtUtc!=at)
                    throw new InvalidDataException("Source-time trade fence was replaced by newer context.");
                var acknowledged=new CapturedEvidence();
                Equal(tradeGreeks,await tradePricing.CalculateAndRetainAsync(original,acknowledged,budget),"queued trade / original C# Greek and digest parity");
                Equal(writer.Value,acknowledged.Value,"complete frozen actual-trade evidence after refresh");
                if(reader.RetainAcknowledgedTrade(original.PhysicalGeneration,original.ContextIdentity,enrichedTrade))throw new InvalidDataException("Old-context trade evidence relabelled as current live enrichment.");
                var expired=original with{QualifiedUntilUtc=at};var expiredWriter=new CapturedEvidence();
                var expiredRisk=await tradePricing.CalculateAndRetainAsync(expired,expiredWriter,budget);
                if(expiredRisk.IsValid||expiredRisk.PricingFailure?.Code!="StaleData"||expiredWriter.Value?.Greeks is not null)
                    throw new InvalidDataException("Expired frozen qualification produced valid risk.");
            }
        }while(consumed>0);
        if(received!=1)throw new InvalidDataException("Actual queued trade was lost or duplicated.");
    }
    try{await runtime.RefreshOptionScopePricingAsync("priced",generation,new('b',64),new('d',64),[new(prepared.Identity,next)],at,budget);throw new InvalidDataException("Stale context CAS was accepted.");}catch(InvalidOperationException){}
    await runtime.ResetOptionScopeAsync("priced",generation,budget);await WaitRisk(next);
    if(reader.TryGetLastTradeWithGreeks(out _)||reader.RetainAcknowledgedTrade(nativeQuote.PhysicalGeneration,prepared.Identity,enrichedTrade))throw new InvalidDataException("Retired physical trade enrichment survived reset.");
    if(!reader.TryGetLastQuoteWithGreeks(out var rebound))throw new InvalidDataException("Stable reader did not follow fresh physical pricing.");
    Equal(Black76PricingModel.Calculate(nextContext,underlying,option,6000,true,at).Value!.Delta,rebound.Greeks.Delta,"stable reader refreshed delta");
    if(!runtime.ReadOptionScopeState("priced",43,3).TryGetOptionStatistics(contract.ContractId,out var restored))throw new InvalidDataException("Option reset lost logical statistics.");
    Equal(saved,restored,"statistics retained through physical reset");
    await runtime.ReleaseOptionScopeAsync("priced",generation,budget);
    if(reader.TryGetLastQuote(out _)||reader.TryGetLastTrade(out _)||reader.TryGetLastQuoteWithGreeks(out _)||reader.TryGetLastTradeWithGreeks(out _))throw new InvalidDataException("Released option reader remained available.");
    if(cache.ReadRisk("priced",generation,next,at)is not null)throw new InvalidDataException("Released pricing cache stayed active.");
    await runtime.StopAndDisposeAsync(budget);await Task.WhenAll(peers).WaitAsync(budget);
    if(Volatile.Read(ref accepted)!=Volatile.Read(ref closed))throw new InvalidDataException("Pricing cleanup left provider sockets open.");
    Console.WriteLine("PASS: SDK -> native scope -> DLL; exact C# selection/IV/risk, stable quote/trade readers, acknowledged actual-trade Greeks; 8 composition modes + 19 failure/timeout comparisons; saved statistics, context CAS, reset/release fences and joined sockets.");
}finally{stopping.Cancel();listener.Stop();await server;try{await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}
sealed class FixedTime(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}

sealed class CapturedEvidence:IOptionTradeEvidenceWriter {
    public OptionTradeEvidence? Value;
    public ValueTask WriteAsync(OptionTradeEvidence evidence,CancellationToken token){token.ThrowIfCancellationRequested();Value=evidence;return ValueTask.CompletedTask;}
}


sealed class FrozenPage(CompositionMarketPage page):ICompositionMarketSource {
    public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request,string? continuation,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(page);}
}


sealed class StalledPage:ICompositionMarketSource {
    public async Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request,string? continuation,CancellationToken token){await Task.Delay(Timeout.Infinite,token);throw new InvalidOperationException("Unreachable");}
}

sealed class CapturedTickPublisher:ITickAggregationEventPublisher {
    public List<FuturesTickTradeDataChangedEvent> Events=[];
    public bool IsRunning=>true;
    public ValueTask StartAsync()=>ValueTask.CompletedTask;
    public ValueTask StopAsync()=>ValueTask.CompletedTask;
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value)=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value){Events.Add(value);return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease)=>ValueTask.CompletedTask;
}

