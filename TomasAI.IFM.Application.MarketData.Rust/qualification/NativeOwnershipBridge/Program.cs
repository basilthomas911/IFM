using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Definitions;
if(args.Length==2&&args[0]=="--decode-owned-ticker"){
 var ticker=MessagePack.MessagePackSerializer.Deserialize<NativeOptionTicker>(await File.ReadAllBytesAsync(args[1])).ToSnapshot();
 if(string.IsNullOrWhiteSpace(ticker.Price.ContractId)||ticker.Price.ValueDate!=new DateOnly(2026,10,27)||ticker.Price.Quote is not{} quote||ticker.Greeks is not{IsValid:true} greeks
    ||quote.SourceSequence!=greeks.OptionPriceSourceSequence)
     throw new InvalidDataException("Native option ticker lost source-coherent pricing during public DTO materialization.");
 Console.WriteLine("PASS native monitored ticker: actual C# DTO materialization with source-coherent quote/Greeks; no managed join or pricing.");return;
}
if(args.Length==2&&args[0]=="--decode-owned-current"){
 var current=MessagePack.MessagePackSerializer.Deserialize<OwnedCurrentFixture>(await File.ReadAllBytesAsync(args[1]));
 if(current.Price is not{} price || string.IsNullOrWhiteSpace(price.ContractId)||price.ValueDate!=new DateOnly(2026,10,27)||(price.Quote is null&&price.Trade is null))
     throw new InvalidDataException("Native SDK market query did not materialize the public source snapshot.");
 if(current.Statistics is{} statistics&&(statistics.ContractId!=price.ContractId||!statistics.HasAnyData))
     throw new InvalidDataException("Native SDK statistics query did not materialize selected native statistics.");
 Console.WriteLine("PASS native current-publication query: actual C# market-price/statistics DTO materialization; original quote/trade continuity and timestamps preserved.");return;
}
if(args.Length==2&&args[0]=="--export-reference-pages") {
 var bytes=Enumerable.Range(0,9000).Select(i=>(byte)(i%251)).ToArray();var facts=new ReferenceFixture(bytes);var storage=new TomasAI.IFM.Application.MarketData.Rust.Persistence.NativeReferenceStorage(facts);
 var frames=new List<string>();var query=new TomasAI.IFM.Application.MarketData.Rust.Persistence.NativeReferenceRequest(77,1,"{}"u8.ToArray(),0);
 for(uint offset=0;offset<9000;offset+=4096){var page=await storage.ReadAsync(query with{Offset=offset},CancellationToken.None);frames.Add(Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(page)));if(offset==0)Array.Fill(bytes,(byte)0);}
 if(facts.Calls!=1)throw new InvalidDataException("Reference paging reread the database.");
 foreach(var invalid in new[]{query with{Kind=0},query with{Offset=1},query with{Offset=12288},query with{Id=88,Offset=4096},query with{Arguments="different"u8.ToArray()}}){try{await storage.ReadAsync(invalid,CancellationToken.None);throw new Exception("Invalid reference query accepted.");}catch(InvalidDataException){}}
 for(ulong id=100;id<107;id++)await storage.ReadAsync(query with{Id=id},CancellationToken.None);
 // The completed original transfer may be reclaimed. None of the eight new,
 // incomplete transfers may be evicted to admit a ninth reader.
 await storage.ReadAsync(query with{Id=108},CancellationToken.None);
 try{await storage.ReadAsync(query with{Id=109},CancellationToken.None);throw new Exception("Unbounded reference cache.");}catch(InvalidOperationException){}
 var pending=await storage.ReadAsync(query with{Id=100,Offset=4096},CancellationToken.None);
 if(pending.Offset!=4096||pending.Total!=9000)throw new InvalidDataException("Cache pressure evicted an incomplete paged transfer.");
 await storage.ReadAsync(query with{Id=100,Offset=8192},CancellationToken.None);
 await storage.ReadAsync(query with{Id=110},CancellationToken.None);
 var protectedPage=await storage.ReadAsync(query with{Id=101,Offset=4096},CancellationToken.None);
 if(protectedPage.Offset!=4096||facts.Calls!=10)throw new InvalidDataException("Completed-transfer reclamation reread or evicted protected snapshots.");
 await File.WriteAllTextAsync(args[1],System.Text.Json.JsonSerializer.Serialize(frames));Console.WriteLine("PASS raw-reference storage: one immutable DB snapshot, exact pages, mutation isolation, request conflicts, offset bounds and bounded cache.");return;
}
if(args.Length==2&&args[0]=="--decode-owned-risk") {
 var risk=MessagePack.MessagePackSerializer.Deserialize<NativeCanonicalQuoteRisk>(await File.ReadAllBytesAsync(args[1]));
 var tick=risk.Quote.ToSnapshot();var greeks=risk.Greeks.ToSnapshot();
 if(!greeks.IsValid||tick.ContractId!="ES20261110C6000"||tick.BidPrice!=99.75m||tick.AskPrice!=100.25m||tick.ReceiveTimestamp==tick.LocalReceivedAtUtc||risk.PhysicalGeneration==Guid.Empty)
  throw new InvalidDataException("Native canonical quote/risk fixture failed its public value contract.");
 Console.WriteLine("PASS actual CLR canonical quote/risk DTO: exact provider/local receipt separation and valid native Greeks.");return;
}
if(args.Length==2&&args[0]=="--export-bootstrap-midpoints") {
 var rows=new List<object>();long[] values=[1,2,3,5,9,10,11,999999999,1000000000,1000000001,6000000000000,6000250000000,long.MaxValue-1,long.MaxValue];
 foreach(var bid in values)foreach(var ask in values.Where(ask=>ask>=bid)) {
  var midpoint=(bid/1000000000m)/2m+(ask/1000000000m)/2m;
  rows.Add(new{bid,ask,bits=decimal.GetBits(midpoint).Select(n=>unchecked((uint)n)).ToArray()});
 }
 await File.WriteAllTextAsync(args[1],System.Text.Json.JsonSerializer.Serialize(rows));
 Console.WriteLine($"PASS exported {rows.Count} actual CLR underlying midpoint outcomes.");return;
}
if(args.Length==2&&args[0]=="--export-native-raw") {
 var cases=new List<object>();var identity=new Guid(77,0,0,new byte[8]);var day=new DateOnly(2026,10,27);
 foreach(var price in new[]{0L,1L,-1L,250000000L,6000125000000L,long.MaxValue,long.MinValue})foreach(var sides in new[]{0,1,2,3})foreach(var quantity in new[]{0u,uint.MaxValue})foreach(var min in new[]{false,true}) {
  var instant=min?DateTimeOffset.MinValue:DateTimeOffset.UnixEpoch.AddTicks(17931024000000001);var received=instant.AddTicks(1);var local=instant.AddTicks(2);
  var trade=new NativeRawTrade("ES20261218",day,price/1_000_000_000m,quantity,long.MaxValue,instant,received);
  var quote=new NativeRawQuote("ES20261218",day,(sides&1)!=0?price/1_000_000_000m:null,quantity,uint.MaxValue,(sides&2)!=0?price/1_000_000_000m:null,quantity,uint.MaxValue,long.MaxValue,instant,received,local);
  if(trade.ToSnapshot().Price!=price/1_000_000_000m||quote.ToSnapshot().LocalReceivedAtUtc!=local)throw new InvalidDataException("Raw query materialization changed data.");
  cases.Add(new{price,sides,quantity,at=instant.UtcTicks-DateTime.UnixEpoch.Ticks,date=day.DayNumber,content=MessagePack.MessagePackSerializer.Serialize(new NativeRawPrices(trade,quote,identity))});
 }
 await File.WriteAllTextAsync(args[1],System.Text.Json.JsonSerializer.Serialize(cases));Console.WriteLine($"PASS exported {cases.Count} actual C# raw-snapshot query wire cases.");return;
}
if(args.Length==3&&args[0]=="--owner-live-discovery") {
 using var ownedInput=System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));var owned=ownedInput.RootElement;
 await using var native=new NativeMarketDataRuntime(Path.GetFullPath(args[1]));
 var ownedAt=DateTimeOffset.UnixEpoch.AddTicks(owned.GetProperty("at").GetInt64());var ownedDate=DateOnly.FromDayNumber(owned.GetProperty("date").GetInt32());
 native.ConfigureApiClock(ownedAt,false);native.ConfigureTreasury(owned.GetProperty("treasury").GetString()!,["127.0.0.1:1"]);native.CreateDatasetOwner();
 var ownedCalendar=new OptionPricingCalendar("fixture/v1","Eastern Standard Time",ownedDate.AddDays(-1),ownedDate.AddDays(30),new(18,0),Enumerable.Range(-1,32).Select(ownedDate.AddDays).ToImmutableArray());
 var ownedPublication=new TreasuryPublicationPolicy("fixture/v1",ownedAt.AddDays(-1),ownedAt.AddDays(1),[new(ownedDate.AddDays(-1),ownedAt.AddDays(-1))],"Eastern Standard Time");
 var ownedPolicy=new NativeOwnerReferencePolicy(ownedPublication,UsTreasuryCurve.ConversionPolicy,new());
 var ownedGateway=new NativeOwnerGateway(new string('x',32),"127.0.0.1",owned.GetProperty("port").GetUInt16(),["127.0.0.1:1"]);
 var ownedBroker=new NativeOwnerBroker(owned.GetProperty("broker").GetString()!,["127.0.0.1:1"]);
 var raw=owned.GetProperty("model");var normalized=new TomasAI.IFM.Shared.MarketData.Definitions.OptionContractDefinition{
  Dataset=raw[14].GetString()!,RawSymbol=raw[17].GetString()!,Ticker=raw[2].GetString()!,Underlying=raw[33].GetString()!,
  Instrument=new(raw[15].GetUInt16(),raw[16].GetUInt32()),Right=(TomasAI.IFM.Shared.MarketData.Definitions.OptionRightSelection)raw[35].GetByte(),
  StrikePrice=decimal.Parse(raw[11].GetString()!,System.Globalization.CultureInfo.InvariantCulture),MaturityDate=ownedDate.AddDays(14),ExpirationTimestampNanoseconds=checked((ulong)raw[21][0].GetInt64()*100)};
 var ownedId=Guid.NewGuid();var ownedRequest=new TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryRequest(ownedId,Guid.Empty,ownedDate,ownedDate.AddDays(14),ownedAt.AddMinutes(2),
  [new(raw[0].GetString()!,raw[29].GetString()!,raw[19].GetString()!,normalized)],true,ownedCalendar,ownedPublication,UsTreasuryCurve.ConversionPolicy,IndependentChainConnection:true);
 native.ConfigureDatasetOwnerDiscovery(ownedRequest,"development",ownedGateway,ownedBroker,ownedPolicy);
 await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(8));
 var admitted=native.ReadDatasetOwnerDiscovery(ownedId);
 if(admitted.Lease is not{} ownedLease||admitted.Failure is not null||admitted.CompleteEmpty||ownedLease.Options.Length!=1||ownedLease.Options[0].Strike!=6000m
  ||ownedLease.Options[0].Pricing.GenerationId!=ownedLease.GenerationId||!ownedLease.IndependentChainConnection)throw new InvalidDataException("Managed native scope admission lost exact contexts/source identity.");
 var beforeAlias=native.ReadDatasetOwnerBackendStatus(true).Datasets.Single(row=>row.Role=="Options");
 var businessPlan=new string('a',64);native.AliasDatasetOwnerScope(ownedId,ownedLease.GenerationId,ownedLease.ScopeId,businessPlan);
 native.AliasDatasetOwnerScope(ownedId,ownedLease.GenerationId,ownedLease.ScopeId,businessPlan);
 var renamed=native.ReadDatasetOwnerDiscovery(ownedId);ownedLease=renamed.Lease??throw new InvalidDataException("Native business alias lost admission.");
 var afterAlias=native.ReadDatasetOwnerBackendStatus(true).Datasets.Single(row=>row.Role=="Options");
 if(ownedLease.ScopeId!=businessPlan||beforeAlias.WorkerInstanceId!=afterAlias.WorkerInstanceId||beforeAlias.GenerationId!=afterAlias.GenerationId)throw new InvalidDataException("Copied business alias replaced physical resources.");
 var captured=await native.CaptureDatasetOwnerCompositionAsync("GLBX.MDP3",new(Guid.NewGuid(),ownedLease.ScopeId,"Daily",ownedLease.GenerationId,default,ownedAt.AddMinutes(2),true,MaximumContracts:1,MaximumQuoteAgeMilliseconds:5000,MaximumQuoteSkewMilliseconds:1000),awaitQuotes:true);
 if(captured.Failure is not null||captured.Snapshot?.Instruments.Length!=1)throw new InvalidDataException("Managed native coherent capture failed.");
 var observed=native.ReadDatasetOwnerBackendStatus(true).Datasets.Single(row=>row.Role=="Options");
 var restored=await native.RecoverDatasetOwnerAsync(new(Guid.NewGuid(),"Options","GLBX.MDP3",observed.WorkerInstanceId,observed.GenerationId,ownedDate,"Offline managed SDK qualification")).WaitAsync(TimeSpan.FromSeconds(8));
 if(!restored.Recovered||!restored.CleanupConfirmed||restored.GenerationId==observed.GenerationId)throw new InvalidDataException("Managed native recovery failed or reused old owner identity.");
 var updated=native.ReadDatasetOwnerDiscovery(ownedId);
 if(updated.Lease?.GenerationId!=restored.GenerationId||updated.Lease.ScopeId!=businessPlan||updated.Lease.Options[0].Pricing.GenerationId!=restored.GenerationId)throw new InvalidDataException("Native scope retained retired reference contexts.");
 native.DisableDatasetOwner(ownedId);await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(8));
 native.StopDatasetOwnerFeeds();await native.WaitDatasetOwnerFeedsStoppedAsync().WaitAsync(TimeSpan.FromSeconds(8));
 Console.WriteLine("PASS actual C# -> DLL -> native-owned SDK/Treasury/NATS/raw-store discovery, exact DTO contexts, coherent capture, physical recovery with fresh contexts, release and joined shutdown.");return;
}
if(args.Length==2&&args[0]=="--owner-discovery") {
 await using var native=new NativeMarketDataRuntime(Path.GetFullPath(args[1]));
 if(!native.SupportsDatasetOwner)throw new InvalidDataException("Incomplete native owner ABI.");
 var discoveryAt=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero);var date=new DateOnly(2026,10,27);
 native.ConfigureApiClock(discoveryAt,false);native.ConfigureTreasury("http://127.0.0.1:9/curve",["127.0.0.1:1"]);native.CreateDatasetOwner();
 var discoveryCalendar=new OptionPricingCalendar("fixture/v1","America/New_York",date.AddDays(-1),date.AddDays(30),new(18,0),Enumerable.Range(-1,32).Select(date.AddDays).ToImmutableArray());
 var discoveryPublication=new TreasuryPublicationPolicy("fixture/v1",discoveryAt.AddDays(-1),discoveryAt.AddDays(1),[new(date.AddDays(-1),discoveryAt.AddDays(-1))]);
 var discoveryPolicy=new NativeOwnerReferencePolicy(discoveryPublication,UsTreasuryCurve.ConversionPolicy,new());
 var gateway=new NativeOwnerGateway(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]);var broker=new NativeOwnerBroker("nats://127.0.0.1:9",["127.0.0.1:1"]);
 var id=Guid.NewGuid();var request=new TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryRequest(id,Guid.Empty,date,date.AddDays(14),discoveryAt.AddMinutes(2),[],false,discoveryCalendar,discoveryPublication,UsTreasuryCurve.ConversionPolicy);
 native.ConfigureDatasetOwnerDiscovery(request,"development",gateway,broker,discoveryPolicy);
 await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(5));
 var first=native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).Identity;
 native.ConfigureDatasetOwnerDiscovery(request,"development",gateway,broker,discoveryPolicy);
 await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(5));
 if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).Identity?.Generation!=first?.Generation)throw new InvalidDataException("Repeated discovery rebuilt unchanged intent.");
 var result=native.ReadDatasetOwnerDiscovery(id);
 if(result.Lease is not null||result.CompleteEmpty||result.Failure?.Code!="IncompleteChain"||!result.Exclusions.IsEmpty)throw new InvalidDataException("Native discovery did not return the exact typed incomplete-chain rejection.");
 await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(5));
 if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Known rejected scope retained feed intent.");
 native.DisableDatasetOwner(id);await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(5));
 if(native.ReadDatasetOwnerDiscovery(id).Failure?.Code!="LeaseEnded")throw new InvalidDataException("Released discovery owner remained usable.");
 native.StopDatasetOwnerFeeds();await native.WaitDatasetOwnerFeedsStoppedAsync().WaitAsync(TimeSpan.FromSeconds(5));
 Console.WriteLine("PASS actual DLL discovery command/query: copied normalized request, typed incomplete-chain failure, idempotent unchanged native generation, release/LeaseEnded and joined shutdown; no managed context preparation or provider acquisition.");return;
}
if(args.Length==2&&args[0]=="--owner-inputs") {
 await using var native=new NativeMarketDataRuntime(Path.GetFullPath(args[1]));
 var ownerAt=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero);var date=new DateOnly(2026,10,27);
 native.ConfigureApiClock(ownerAt,false);native.ConfigureTreasury("http://127.0.0.1:9/curve",["127.0.0.1:1"]);native.CreateDatasetOwner();Console.WriteLine("TRACE owner created");
 var broker=new NativeOwnerBroker("nats://127.0.0.1:9",["127.0.0.1:1"]);
 native.ConfigureHistory("http://127.0.0.1:9/",new string('x',32),["127.0.0.1:1"]);
 var bootstrapTemplate=new NativeOwnerSource(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"],[new(["ES.FUT"],"trades","raw_symbol")],[],false);
 native.DiscoverDatasetOwnerFutures(MarketDataLane.FuturesGlbx,date,date,"development",1,bootstrapTemplate,broker,[new("ES",1,"USD",true)],sessionReplay:false);
 Console.WriteLine("TRACE native futures discovery accepted without caller-resolved registrations");

 NativeRegistration future=new(42,"ES20261218","42",false,250000000,false,true,3);
 NativeRegistration option=new(43,"ES20261110C6000","43",true,null,false,true,3);
 var source=new NativeOwnerSource(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"],[new(["42"],"mbp-1","instrument_id")],[future],false);
 native.StartDatasetOwnerFutures(MarketDataLane.FuturesGlbx,date,date,"development",1,source,broker);Console.WriteLine("TRACE futures accepted");
 var ownerCalendar=new OptionPricingCalendar("fixture/v1","America/New_York",date.AddDays(-30),date.AddDays(45),new(18,0),Enumerable.Range(-30,76).Select(date.AddDays).Where(d=>d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
 var convention=new OptionPricingConvention{SchemaVersion=3,ContractId=option.Contract,Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=43,RawSymbol="fixture-option",Root="ES",Exchange="XCME",Currency="USD",UnderlyingContractId=future.Contract,
  ExerciseStyle=OptionExerciseStyle.European,SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=ownerAt.AddDays(14),LastTradingUtc=ownerAt.AddDays(14),DayCount=PricingDayCount.Actual365Fixed,
  CalendarVersion=ownerCalendar.Version,Multiplier=50,TickSize=.25m,TickRuleVersion="fixture-tick/v1",DefinitionDigest=new('a',64),MappingVersion="fixture/v1",EvidenceId="fixture",EffectiveFromUtc=ownerAt.AddDays(-30),EffectiveUntilUtc=ownerAt.AddDays(45),
  PremiumStyle=OptionPremiumStyle.PaidUpfront,UnderlyingKind=PricingUnderlyingKind.Futures,Strike=6000,Right=PricingOptionRight.Call,PremiumTickRule=OptionPremiumTickRule.Fixed};
 var ownerPublication=new TreasuryPublicationPolicy("fixture/v1",ownerAt.AddDays(-1),ownerAt.AddDays(1),[new(date.AddDays(-1),ownerAt.AddDays(-1))]);
 var refs=new NativeReviewedScopeReferences(ownerCalendar,ownerPublication,UsTreasuryCurve.ConversionPolicy,[new(convention,42,6000,true,true)],new OptionPricingRefreshPolicy(),0,false);
 var options=source with{Subscriptions=[new(["42","43"],"mbp-1","instrument_id")],Registrations=[future,option],Observations=true};
 native.ConfigureDatasetOwnerCatalog(date,date,"development",options,broker,refs);Console.WriteLine("TRACE catalog accepted");
 using var rawInput=System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync("TomasAI.IFM.Application.MarketData.Rust/qualification/ContextWireBridge/reviewed-options.json"));
 var rawContract=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel>(Convert.FromBase64String(rawInput.RootElement[0].GetProperty("content").GetString()!));
 native.ConfigureDatasetOwnerRawCatalog(date,date,"development",new NativeOwnerGateway(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]),broker,new NativeOwnerReferencePolicy(ownerPublication,UsTreasuryCurve.ConversionPolicy,new()),[rawContract]);
 Console.WriteLine("TRACE raw model accepted without managed contexts, calendars, dependencies or subscription plans");

 native.ConfigureDatasetOwnerScope("trade-1",[43],[43],[]);Console.WriteLine("TRACE scope accepted");var business=Guid.NewGuid();
 native.EnableDatasetOwner(business,["trade-1"]);Console.WriteLine("TRACE owner enabled");native.EnableDatasetOwner(business,["trade-1"]);Console.WriteLine("TRACE owner enabled");
 var secondContract=rawContract with{ContractId="ES20261110P6000",InstrumentId=44,RawSymbol="fixture-put",OptionType="Put",OptionRight=TomasAI.IFM.Domain.MarketData.Shared.ViewModels.ReferenceOptionRight.Put};
 native.ConfigureDatasetOwnerRawCatalog(date,date,"development",new NativeOwnerGateway(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]),broker,new NativeOwnerReferencePolicy(ownerPublication,UsTreasuryCurve.ConversionPolicy,new()),[secondContract]);
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Raw catalog addition lost existing trade intent.");
 Console.WriteLine("TRACE second raw contract merged while the first owner remained enabled");
 native.DisableDatasetOwner(business);Console.WriteLine("TRACE owner disabled");native.DisableDatasetOwner(business);Console.WriteLine("TRACE owner disabled");
 native.StopDatasetOwnerFeeds();
 var monitoredOwner=new TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner("Trade","fixture-1","put");
 var secondOwner=monitoredOwner with{WorkflowId="fixture-2"};
 var monitored=new TomasAI.IFM.Application.MarketData.Rust.Api.NativeOwnedOptionFeeds(native,new(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]),broker,new(ownerPublication,UsTreasuryCurve.ConversionPolicy,new()),"development");
 using(var canceled=new CancellationTokenSource()) {
  canceled.Cancel();try{await monitored.AcquireAsync(monitoredOwner,secondContract,date,date,canceled.Token);throw new InvalidDataException("Canceled caller accepted demand.");}catch(OperationCanceledException){}
  if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Pre-cancelled request changed native intent.");
 }
 foreach(var owner in new[]{monitoredOwner,monitoredOwner,secondOwner})monitored.Accept(owner,secondContract,date,date);
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Native monitored command did not accept intent.");
 monitored.Release(monitoredOwner,secondContract.ContractId.ToString());
 monitored.Release(monitoredOwner,secondContract.ContractId.ToString());
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Releasing one monitored owner removed the other.");
 monitored.Release(secondOwner,secondContract.ContractId.ToString());
 if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Last monitored release left desired option demand.");
 Console.WriteLine("TRACE single-call monitored acquisition and idempotent shared-owner release passed");
 var scopeOwner=Guid.NewGuid();
 try{native.ClaimDatasetOwnerScope(Guid.Empty,"atomic-rejected",[44],[44],[]);throw new InvalidDataException("Invalid owner acquired a scope.");}
 catch(InvalidOperationException error)when(error.Message.Contains("ABI status 1",StringComparison.Ordinal)){}
 if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Rejected scope claim changed desired work.");
 native.ClaimDatasetOwnerScope(scopeOwner,"atomic-scope",[44],[44],[]);
 native.ClaimDatasetOwnerScope(scopeOwner,"atomic-scope",[44],[44],[]);
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Atomic scope command failed to accept work.");
 native.DisableDatasetOwner(scopeOwner);
 try{native.ClaimDatasetOwnerScope(scopeOwner,"expired-scope",[44],[44],[],ownerAt.AddSeconds(-1));throw new InvalidDataException("Expired native scope accepted demand.");}
 catch(InvalidOperationException error)when(error.Message.Contains("ABI status 1",StringComparison.Ordinal)){}
 if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Expired scope left active demand.");
 Console.WriteLine("TRACE atomic scope/claim and native-clock expiry validation passed");
 var rawScope=new NativeOwnerRawScope(scopeOwner,"raw-complete-scope",true,"Daily");
 native.ConfigureDatasetOwnerRawCatalog(date,date,"development",new(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]),broker,new(ownerPublication,UsTreasuryCurve.ConversionPolicy,new()),[secondContract],scopeClaim:rawScope);
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Raw complete scope did not derive native membership and demand.");
 native.DisableDatasetOwner(scopeOwner);
 Console.WriteLine("TRACE complete raw scope derived its membership and cache in Rust");
 native.StopDatasetOwnerFeeds();
 native.ConfigureDatasetOwnerBackground(date,date,"development",new(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]),broker,new(ownerPublication,UsTreasuryCurve.ConversionPolicy,new()),new("Development"));
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Native background configuration did not accept desired discovery.");
 native.StopDatasetOwnerFeeds();await native.WaitDatasetOwnerFeedsStoppedAsync();
 if(native.ReadDatasetOwnerPublication(MarketDataLane.OptionsGlbx).HasDesired)throw new InvalidDataException("Explicit stop retained native background intent.");
 Console.WriteLine("TRACE native background demand needs no resolved models and stops without managed cleanup");



 native.StopDatasetOwnerFeeds();
 TomasAI.IFM.Application.MarketData.Rust.Api.NativeOwnedFuturesDemand Demand(MarketDataLane lane,string root,uint count)=>new(lane,new(new string('x',32),"127.0.0.1",9,["127.0.0.1:1"]),[new(root,count,null,false)]);
 try{native.StartDatasetOwnerFutures(date,date,"development",broker,[Demand(MarketDataLane.FuturesGlbx,"ES",1),Demand(MarketDataLane.FuturesXcbf,"VX",0)]);throw new InvalidDataException("Invalid second dataset accepted.");}catch(InvalidOperationException error)when(error.Message.Contains("ABI status 1",StringComparison.Ordinal)){}
 if(native.ReadDatasetOwnerPublication(MarketDataLane.FuturesGlbx).HasDesired || native.ReadDatasetOwnerPublication(MarketDataLane.FuturesXcbf).HasDesired)throw new InvalidDataException("Invalid batch partially started the first dataset.");
 native.StartDatasetOwnerFutures(date,date,"development",broker,[Demand(MarketDataLane.FuturesGlbx,"ES",1),Demand(MarketDataLane.FuturesXcbf,"VX",2)]);
 if(!native.ReadDatasetOwnerPublication(MarketDataLane.FuturesGlbx).HasDesired || !native.ReadDatasetOwnerPublication(MarketDataLane.FuturesXcbf).HasDesired)throw new InvalidDataException("Valid native batch did not accept both datasets.");
 if(native.ReadRetiredDatasetEvidence().RetainedGenerations>4)throw new InvalidDataException("Retired evidence accounting exceeds capacity.");
 Console.WriteLine("TRACE native root batch: full validation before acceptance, independent desired lanes, bounded evidence accounting");

 native.StopDatasetOwnerFeeds();
 if(source.ToString().Contains(source.Key,StringComparison.Ordinal)||broker.ToString().Contains(broker.Endpoint,StringComparison.Ordinal))throw new InvalidDataException("Credential configuration leaked through ToString.");
 Console.WriteLine("PASS actual DLL/C# typed owner commands: futures/catalog/scope, idempotent durable enable/disable, stop and credential redaction. Readiness is deliberately not claimed for unreachable fixture endpoints.");return;
}
if(args.Length==3&&args[0]=="--current-only") {
 await using var native=new NativeMarketDataRuntime(Path.GetFullPath(args[1]));native.ConfigureCatalog(100,1024*1024);
 using var fixture=System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));
 var basis=fixture.RootElement[0].GetProperty("json").GetString()!;
 async IAsyncEnumerable<CatalogSourceLine> Lines() {
  foreach(var (symbol,maturity,instrument) in new[]{("ESZ6",new DateOnly(2026,12,18),42u),("ESH7",new DateOnly(2027,3,19),43u)}) {
   var row=System.Text.Json.Nodes.JsonNode.Parse(basis)!;row["raw_symbol"]=symbol;row["hd"]!["instrument_id"]=instrument;
   row["maturity_year"]=maturity.Year;row["maturity_month"]=maturity.Month;row["maturity_day"]=maturity.Day;
   row["expiration"]=checked((ulong)(new DateTimeOffset(maturity.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero).UtcTicks-DateTime.UnixEpoch.Ticks)*100).ToString(System.Globalization.CultureInfo.InvariantCulture);
   yield return new("GLBX.MDP3",row.ToJsonString());await Task.Yield();
  }
 }
 await native.HydrateCatalogAsync(Lines());using var query=native.OpenCatalogSnapshot();
 var selected=query.SelectCurrentFutures(" es ",new(2026,8,14),2,null);
 if(selected.Length!=2||selected[0].ContractId!="ES20261218"||selected[1].ContractId!="ES20270319"||!selected[0].OnTheRun||selected[1].OnTheRun||selected.Any(x=>!x.Rollover))throw new InvalidDataException("Actual DLL current-futures selection mismatch.");
 try{query.SelectCurrentFutures("ES",new(2026,8,14),3,null);throw new InvalidDataException("Missing contracts were invented.");}
 catch(TomasAI.IFM.Application.MarketData.Contracts.OnTheRunFuturesContractNotFoundException){}
 Console.WriteLine("PASS actual DLL/C# current futures: native eligibility/order/DTOs, current/next flags and typed missing-contract failure.");return;
}
if(args.Length==2&&args[0]=="--verify-native-recovery") {
 using var input=System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
 var bytes=Convert.FromBase64String(input.RootElement.GetProperty("content").GetString()!);
 var options=MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData);
 var value=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent>(bytes,options);
 if(value.Subject.ToString()!=input.RootElement.GetProperty("subject").GetString() || value.Id==Guid.Empty || value.CommandId!=value.Id
  || value.EntityId.ValueDate!=new DateOnly(2026,10,27) || value.Dataset!="GLBX.MDP3" || value.Role!="Options" || value.ErrorCode!=503
  || value.ErrorType!=TomasAI.IFM.Shared.EventSourcing.ErrorType.System || !value.ErrorMessage.Contains("recovery failed after 120 seconds")
  || !MessagePack.MessagePackSerializer.Serialize(value,options).SequenceEqual(bytes))throw new InvalidDataException("Native terminal event differs from existing UI contract.");
 Console.WriteLine("PASS actual C# terminal UI event: exact twenty-key wire, subject, ValueDate, identity and error metadata.");return;
}
if(args.Length==2&&args[0]=="--verify-native-diagnostics") {
 var frames=System.Text.Json.JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[1]))!;
 var options=MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData);
 for(int i=0;i<frames.Length;i++) {
  var bytes=Convert.FromBase64String(frames[i]);
  var record=MessagePack.MessagePackSerializer.Deserialize<NativeProviderDiagnostic>(bytes,options);
  if(!MessagePack.MessagePackSerializer.Serialize(record,options).SequenceEqual(bytes))throw new InvalidDataException("Native diagnostic round trip differs; log acknowledgment hash would be unsafe.");
  if(record.Code!=(i==0?250u:199u)||record.IsLast!=(i!=0)||record.Worker==Guid.Empty)throw new InvalidDataException("Unknown provider code or source worker lost.");
  var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
  var receipt=new NativeDiagnosticReceipt(1,digest,digest,true,null);
  var decoded=MessagePack.MessagePackSerializer.Deserialize<NativeDiagnosticReceipt>(MessagePack.MessagePackSerializer.Serialize(receipt,options),options);
  if(decoded!=receipt)throw new InvalidDataException("Native diagnostic receipt parity failed.");
 }
 Console.WriteLine("PASS actual C# diagnostic DTO: exact native wire, unknown system/error codes, source workers and receipt hash basis.");
 return;
}
if(args.Length==2&&args[0]=="--owner-only") {
 await using var native=new NativeMarketDataRuntime(Path.GetFullPath(args[1]),2);
 if(!native.SupportsDatasetOwner)throw new InvalidDataException("Native owner exports are incomplete.");
 native.ConfigureApiClock(new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero),false);
 native.CreateDatasetOwner();
 await native.WaitDatasetOwnerFuturesReadyAsync();
 foreach(var lane in new[]{MarketDataLane.FuturesGlbx,MarketDataLane.FuturesXcbf,MarketDataLane.OptionsGlbx}) {
  var deadline=System.Diagnostics.Stopwatch.StartNew();
  while(native.ReadDatasetOwnerHealth(lane).Phase!=3) {
   if(deadline.Elapsed>TimeSpan.FromSeconds(2))throw new TimeoutException("Native idle owner readiness.");
   await Task.Yield(); // Qualification-only retry; no production polling service.
  }
 }
 await Task.WhenAll(new[]{MarketDataLane.FuturesGlbx,MarketDataLane.FuturesXcbf,MarketDataLane.OptionsGlbx}.Select(lane=>native.WaitDatasetOwnerReadyAsync(lane,requireActive:false)));
 try{await native.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx);throw new Exception("Idle readiness satisfied active demand.");}catch(InvalidOperationException error)when(error.Message.Contains("withdrawn")){}
 if(native.ReadDatasetOwnerState(MarketDataLane.FuturesGlbx,42,3).Flags!=0)throw new InvalidDataException("Idle native owner invented market data.");
 if(native.ReadDatasetOwnerQuoteGreeks(42,3) is not null)throw new InvalidDataException("Idle native owner invented quote Greeks.");
 if(native.ReadDatasetOwnerTradeGreeks(42,3) is not null)throw new InvalidDataException("Idle native owner invented trade Greeks.");
 if(native.ReadDatasetOwnerContracts(MarketDataLane.FuturesGlbx).Length!=0||native.ReadDatasetOwnerContracts(MarketDataLane.FuturesXcbf).Length!=0)throw new InvalidDataException("Idle native owner invented current contracts.");
 if(native.ReadDatasetOwnerCurrent(MarketDataLane.OptionsGlbx).Length!=0)throw new InvalidDataException("Idle native owner invented a current publication.");
 if(native.ReadDatasetOwnerBinding("ES20261218",false) is not null||native.ReadDatasetOwnerPrice("ES20261218",false,TimeSpan.FromMinutes(15)) is not {Mapped:false,Price:null,Failure:null})throw new InvalidDataException("Native idle canonical query invented market data.");
 if(native.ReadDatasetOwnerQuoteRisk("ES20261110C6000",new(2026,10,27)) is not null||native.ReadDatasetOwnerTradeRisk("ES20261110C6000",new(2026,10,27)) is not null)throw new InvalidDataException("Native idle canonical risk query invented Greeks.");
 var detachedOption=new NativeOwnedOptionPriceReader(native,"ES20261110C6000",new(2026,10,27),42,3);
 if(detachedOption.TryGetLastTrade(out _) || detachedOption.TryGetLastQuote(out _) || detachedOption.TryGetLastTradeWithGreeks(out _) || detachedOption.TryGetLastQuoteWithGreeks(out _))throw new InvalidDataException("Idle managed query reader invented an observation.");

 var nativeStatus=native.ReadDatasetOwnerBackendStatus(true);
 if(!nativeStatus.Ready||nativeStatus.ValueDate is not null||nativeStatus.Datasets.Any(d=>d.Running||!d.Healthy||d.Lifecycle!="Idle"||d.ProcessId!=Environment.ProcessId||d.ChangedAtUtc!=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero)))throw new InvalidDataException("Native complete backend-status wire differs from current CLR DTO semantics.");
 var stableStatus=native.ReadDatasetOwnerBackendStatus(true);
 if(stableStatus.HostInstanceId!=nativeStatus.HostInstanceId||stableStatus.Revision!=nativeStatus.Revision)throw new InvalidDataException("Unchanged native status changed identity/revision.");
 if(native.ReadDatasetOwnerMarketPrice("unknown") is not null || native.ReadDatasetOwnerStatistics("unknown") is not null)
     throw new InvalidDataException("Idle native publication-state queries invented snapshots.");
 var routeOwner=new TickerStreamOwner("fixture","owner","leg");
 if(native.ChangeDatasetOwnerRoute(routeOwner,"unknown",false,false)||native.IsDatasetOwnerStreamActive("unknown"))
     throw new InvalidDataException("Idle route release/health invented an owner.");
 try{native.ChangeDatasetOwnerRoute(routeOwner,"unknown",false,true);throw new InvalidDataException("Unmapped native route accepted.");}
 catch(InvalidOperationException error)when(error.Message.Contains("ABI status 1",StringComparison.Ordinal)){}
 var nativeSnapshot=native.ReadDatasetOwnerSnapshot(true);
 if(nativeSnapshot.Status.HostInstanceId!=nativeStatus.HostInstanceId||nativeSnapshot.Manifests.Length!=0||nativeSnapshot.Contracts.Length!=0||nativeSnapshot.CurrentValues.Length!=0||nativeSnapshot.OptionChains?.Length!=0||nativeSnapshot.OptionObservations?.Length!=0||nativeSnapshot.NativeOptionWorkload is not{ReplacementPending:false,Scopes.Length:0,FormatVersion:1,NativeResourceProofsComplete:false})throw new InvalidDataException("Native complete catch-up wire invented live data or workload.");
 var recoveryRequest=new TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryRequest(Guid.NewGuid(),"Options","GLBX.MDP3",Guid.Empty,Guid.Empty,new(2026,10,27),"idle qualification");
 var rejectedRecovery=await native.RecoverDatasetOwnerAsync(recoveryRequest);
 if(rejectedRecovery.Recovered||rejectedRecovery.CleanupConfirmed||rejectedRecovery.LiveFeedEnabled||string.IsNullOrWhiteSpace(rejectedRecovery.Error))throw new InvalidDataException("Native invalid-date recovery result incorrectly certified recovery.");
 var captureAt=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero);
 var captureRequest=new TomasAI.IFM.Application.MarketData.Pricing.CompositionSnapshotRequest(Guid.Empty,"trade","Daily",Guid.NewGuid(),captureAt,captureAt.AddSeconds(1),true);
 var invalidCapture=await native.CaptureDatasetOwnerCompositionAsync("GLBX.MDP3",captureRequest);
 if(invalidCapture.Snapshot is not null||invalidCapture.Failure?.Code!="InvalidSnapshotRequest")throw new InvalidDataException("Native malformed snapshot request lost its typed failure.");
 var idleCapture=await native.CaptureDatasetOwnerCompositionAsync("GLBX.MDP3",captureRequest with{SnapshotId=Guid.NewGuid()});
 if(idleCapture.Snapshot is not null||idleCapture.Failure?.Code!="LeaseEnded")throw new InvalidDataException("Native idle scope invented a coherent snapshot.");
 Console.WriteLine("TRACE native recovery and composition typed result wire passed without managed orchestration");
 var notReadyStatus=native.ReadDatasetOwnerBackendStatus(false);
 if(notReadyStatus.Ready||notReadyStatus.Revision<=stableStatus.Revision)throw new InvalidDataException("Host RPC transport readiness was not observed independently.");
 var ownedCache=new TomasAI.IFM.Application.MarketData.Rust.Api.NativeOwnedOptionChainCache(native);
 var missingPrepared=ownedCache.TryGetSnapshot(new("trade-1",Guid.Empty,new DateOnly(2026,10,27),"Daily",new DateTimeOffset(2026,10,27,12,1,0,TimeSpan.Zero)){ConfigurationDigest="fixture"});
 if(missingPrepared.ReasonCode!="ScopeNotReady"||missingPrepared.IsReady||ownedCache.GetStatus(new("trade-1",Guid.Empty)).ReasonCode!="ScopeNotReady"||ownedCache.GetStatus(new(" ",Guid.Empty)).ReasonCode!="InvalidScope")throw new InvalidDataException("Native query-only cache returned unexpected idle status.");
 if(native.ReadDatasetOwnerCache("trade-1") is not null)throw new InvalidDataException("Idle native owner invented a prepared cache.");
 native.CommandDatasetOwner("{\"command\":\"stop\"}"u8);
 if(native.ReadDatasetOwnerFailures().Length!=0)throw new InvalidDataException("Idle control invented a terminal failure.");
 try{native.ConfigureLive(new(2026,10,27),Guid.NewGuid(),new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero),false,16);throw new InvalidDataException("Prototype ownership overlapped the new owner.");}
 catch(InvalidOperationException error)when(error.Message.Contains("ABI status 7",StringComparison.Ordinal)){}
 await native.DisposeAsync();
 Console.WriteLine("PASS actual DLL/C# owner ABI: three idle datasets, command/query health, bounded journal, prototype exclusion and awaited native disposal.");
 return;
}
var library=Path.GetFullPath(args[0]);var wire=await File.ReadAllBytesAsync(args[1]);
var baseAt=new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);var at=baseAt.AddSeconds(1);var budget=TimeSpan.FromSeconds(5);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();int accepted=0,closed=0,stallNext=0,maxWireLines=0;var closures=System.Threading.Channels.Channel.CreateBounded<int>(16);
var server=Task.Run(async()=>{try{while(true){var client=await listener.AcceptTcpClientAsync(stopping.Token);Interlocked.Increment(ref accepted);
    peers.Add(Task.Run(async()=>{using(client){try{var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
        await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);
        if(await reader.ReadLineAsync(stopping.Token)is null)return;await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);
        string? line;int count=0;var completedSubscriptions=new HashSet<string>(StringComparer.Ordinal);
        while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session"){
            count++;var fields=line.Split('|').Select(x=>x.Split('=',2)).ToDictionary(x=>x[0],x=>x[1],StringComparer.Ordinal);
            if(fields["is_last"]=="1")completedSubscriptions.Add(fields["id"]);
        }
        Interlocked.Exchange(ref maxWireLines,Math.Max(count,Volatile.Read(ref maxWireLines)));
        if(line is null)return;if(!completedSubscriptions.SetEquals(["1","2","3"]))throw new InvalidDataException("Expected three complete logical subscriptions, including all SDK chunks.");
        if(Interlocked.Exchange(ref stallNext,0)==0)await stream.WriteAsync(wire,stopping.Token);if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected provider cleanup EOF.");
    }catch(IOException){}finally{closures.Writer.TryWrite(Interlocked.Increment(ref closed));}}}));
}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
Console.WriteLine("TRACE: native runtime creation");
await using var runtime=new NativeMarketDataRuntime(library,2);
Console.WriteLine("TRACE: configure and prepare native options owner");
runtime.ConfigureLive(new(2026,9,8),Guid.NewGuid(),at,false,64);await runtime.PrepareOptionOwnerAsync(Guid.Empty,budget);
var generation=runtime.ReadOptionOwnerGeneration();
if(!runtime.SupportsNativeScopeClaims)throw new InvalidDataException("Complete native claim exports missing.");
var calendar=new OptionPricingCalendar("fixture-calendar/v1","America/New_York",new(2026,1,1),new(2026,12,31),new(18,0),
    Enumerable.Range(0,365).Select(i=>new DateOnly(2026,1,1).AddDays(i)).Where(d=>d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
var contract=new OptionPricingConvention{SchemaVersion=3,ContractId="ES20261002C6000",Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=43,
    RawSymbol="fixture-option",Root="ES",Exchange="XCME",Currency="USD",UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,
    SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=baseAt.AddDays(24),LastTradingUtc=baseAt.AddDays(24),DayCount=PricingDayCount.Actual365Fixed,
    CalendarVersion=calendar.Version,Multiplier=50,TickSize=.25m,TickRuleVersion="fixture-tick/v1",DefinitionDigest=new('a',64),MappingVersion="fixture-mapping/v1",
    EvidenceId="synthetic-series",EffectiveFromUtc=baseAt.AddDays(-10),EffectiveUntilUtc=baseAt.AddDays(40),PremiumStyle=OptionPremiumStyle.PaidUpfront,
    UnderlyingKind=PricingUnderlyingKind.Futures,Strike=6000,Right=PricingOptionRight.Call,PremiumTickRule=OptionPremiumTickRule.Fixed};

var http=new TcpListener(IPAddress.Loopback,0);http.Start();var endpoint=$"http://127.0.0.1:{((IPEndPoint)http.LocalEndpoint).Port}/curve.xml";
var httpPeers=new ConcurrentBag<Task>();
var httpServer=Task.Run(async()=>{try{while(true){var client=await http.AcceptTcpClientAsync(stopping.Token);httpPeers.Add(Task.Run(async()=>{using(client){
 var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);var line=await reader.ReadLineAsync(stopping.Token)??throw new InvalidDataException("HTTP request missing");
 var month=System.Text.RegularExpressions.Regex.Match(line,"field_tdr_date_value_month=([0-9]{6})").Groups[1].Value;
 if(month.Length!=6)throw new InvalidDataException("Native Treasury month missing");
 while(await reader.ReadLineAsync(stopping.Token)is{}header&&header.Length>0){}
 var body=$"<feed xmlns='http://www.w3.org/2005/Atom' xmlns:d='http://schemas.microsoft.com/ado/2007/08/dataservices' xmlns:m='http://schemas.microsoft.com/ado/2007/08/dataservices/metadata'><entry><content><m:properties><d:NEW_DATE>{month[..4]}-{month[4..]}-01T00:00:00</d:NEW_DATE><d:BC_1MONTH>5.00</d:BC_1MONTH><d:BC_2MONTH>5.10</d:BC_2MONTH><d:BC_3MONTH>5.2</d:BC_3MONTH></m:properties></content></entry></feed>";
 await stream.WriteAsync(Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}"),stopping.Token);
}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
runtime.ConfigureTreasury(endpoint,["127.0.0.1:1"]);
var publication=new TreasuryPublicationPolicy("native-publication/v1",baseAt.AddDays(-1),baseAt.AddDays(1),[new(new(2026,9,1),baseAt.AddDays(-1))]);
var policy=new OptionPricingRefreshPolicy{PriceDeltaMilliseconds=10,FullRiskMilliseconds=20};
var references=new NativeReviewedScopeReferences(calendar,publication,UsTreasuryCurve.ConversionPolicy,[new(contract,42,6000,true,true)],policy,0,false);
NativeSubscription[] subscriptions=[new(["42","43"],"trades","instrument_id"),new(["42","43"],"mbp-1","instrument_id"),new(["43"],"statistics","instrument_id")];
NativeRegistration[] registrations=[new(42,contract.UnderlyingContractId,"42",false,null,false,true,3),new(43,contract.ContractId,"43",true,null,false,true,3)];
async Task Closed(int count){using var deadline=new CancellationTokenSource(budget);while(Volatile.Read(ref closed)<count)await closures.Reader.ReadAsync(deadline.Token);}
var read=typeof(NativeMarketDataRuntime).GetMethod("ReadScopeCalculation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
uint RiskFlags(){var value=read.Invoke(runtime,["reviewed",generation,43u,true])!;return (uint)value.GetType().GetField("Flags",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(value)!;}
try {
 Console.WriteLine("TRACE: reviewed-scope import");
 await runtime.StartReviewedOptionScopeAsync("reviewed",new('a',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,registrations,references,budget);
 using(var deadline=new CancellationTokenSource(budget)){while((RiskFlags()&20)!=4)await Task.Delay(5,deadline.Token);}
 var durable=runtime.RetainNativeOptionScopes(generation,Guid.NewGuid(),["reviewed"]);
 var same=runtime.RetainNativeOptionScopes(generation,durable.Owner,["reviewed"]);if(same!=durable)throw new InvalidDataException("Identical native intent changed token.");
 var temporary=runtime.RetainNativeOptionScopes(generation,Guid.NewGuid(),["reviewed"],TimeSpan.FromMilliseconds(200));
 runtime.RenewNativeOptionScopes(temporary,TimeSpan.FromMilliseconds(200));
 await Task.Delay(250);if(runtime.ReadOptionScopeStatus("reviewed").Phase!=3)throw new InvalidDataException("Temporary expiry removed a durable shared scope.");
 try {runtime.RenewNativeOptionScopes(temporary,TimeSpan.FromSeconds(1));throw new InvalidDataException("Expired token renewed.");}catch(InvalidOperationException e)when(e.Message.Contains("ABI status",StringComparison.Ordinal)){}
 var before=Volatile.Read(ref accepted);
 try {await runtime.StartReviewedOptionScopeAsync("invalid",new('b',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,registrations,references with{Options=[new(contract,42,-1,true,true)]},budget);throw new InvalidDataException("Invalid native strike admitted.");}catch(InvalidOperationException e)when(e.Message.Contains("strike",StringComparison.OrdinalIgnoreCase)){}
 if(Volatile.Read(ref accepted)!=before)throw new InvalidDataException("Invalid native batch opened a provider connection.");
 Interlocked.Exchange(ref stallNext,1);using var cancellation=new CancellationTokenSource();
 var blocked=runtime.StartOptionScopeAsync("blocked",new('c',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,registrations,true,budget,cancellation.Token);
 using(var deadline=new CancellationTokenSource(budget)){while(Volatile.Read(ref accepted)==before)await Task.Delay(5,deadline.Token);}
 var started=System.Diagnostics.Stopwatch.GetTimestamp();runtime.ReleaseNativeOptionScopes(durable);
 if(System.Diagnostics.Stopwatch.GetElapsedTime(started)>TimeSpan.FromMilliseconds(200))throw new InvalidDataException("Native release waited behind provider control admission.");
 await Closed(1);if(blocked.IsCompleted)throw new InvalidDataException("Unrelated stalled setup ended before explicit cancellation.");
 cancellation.Cancel();try{await blocked;throw new InvalidDataException("Cancelled setup completed successfully.");}catch(OperationCanceledException){}
 await runtime.ReleaseOptionScopeAsync("blocked",generation,budget);await Closed(2);
 await runtime.PrepareOptionOwnerAsync(generation,budget);var replacement=runtime.ReadOptionOwnerGeneration();if(replacement==generation)throw new InvalidDataException("Owner generation did not change.");
 runtime.ReleaseNativeOptionScopes(durable);runtime.ReleaseNativeOptionScopes(temporary);
 try{runtime.RenewNativeOptionScopes(durable,TimeSpan.FromSeconds(1));throw new InvalidDataException("Old generation renewed.");}catch(InvalidOperationException e)when(e.Message.Contains("ABI status",StringComparison.Ordinal)){}
 await runtime.StopOptionOwnerAsync(replacement,budget);await runtime.StopAndDisposeAsync(budget);
 Console.WriteLine("PASS: thin reviewed-scope FFI; native HTTP/context/manifests/risk; shared durable and ephemeral intent, expiry/non-resurrection, malformed definitions, blocked provider admission, independent release/socket closure, retired-generation fencing and joined disposal.");
} finally {stopping.Cancel();listener.Stop();http.Stop();await server;await httpServer;await Task.WhenAll(peers);await Task.WhenAll(httpPeers);}

sealed class ReferenceFixture(byte[] bytes):TomasAI.IFM.Application.MarketData.Rust.Persistence.INativeReferenceFacts
{
 public int Calls;
 public Task<byte[]> ReadAsync(byte kind,ReadOnlyMemory<byte> arguments,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();Calls++;return Task.FromResult(bytes);}
}

[MessagePack.MessagePackObject]
public sealed record OwnedCurrentFixture(
 [property:MessagePack.Key(0)]TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceSnapshot? Price,
 [property:MessagePack.Key(1)]TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesSessionStatisticsSnapshot? Statistics);
