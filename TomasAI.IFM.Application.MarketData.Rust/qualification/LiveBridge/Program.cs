using System.Net;
using System.Net.Sockets;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

var path=Path.GetFullPath(args[0]);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
int accepted=0,closed=0;
using var stop=new CancellationTokenSource();
var clients=new List<Task>();
var server=Task.Run(async()=>{try{while(true){var socket=await listener.AcceptTcpClientAsync(stop.Token);Interlocked.Increment(ref accepted);clients.Add(Task.Run(async()=>{using(socket){var buffer=new byte[1];if(await socket.GetStream().ReadAsync(buffer)!=0)throw new Exception("Unexpected unauthenticated fixture data.");Interlocked.Increment(ref closed);}}));}}catch(OperationCanceledException){}});
for(var cycle=0;cycle<100;cycle++){
    await using var runtime=new NativeMarketDataRuntime(path,1);
    runtime.ConfigureLive(new(2026,10,26),Guid.NewGuid(),DateTimeOffset.UnixEpoch,false,8);
    runtime.UpdateLiveClock(DateTimeOffset.UnixEpoch.AddTicks(42),false);
    if(runtime.ReadLiveStatus(MarketDataLane.OptionsGlbx).Phase!=0)throw new Exception("Unstarted lane was visible.");
    var absent=runtime.ReadLiveObservation(MarketDataLane.OptionsGlbx,"ES20261218C6000");
    if(absent.Flags!=0)throw new Exception("Unstarted price was visible.");
    using var cancel=new CancellationTokenSource();
    var before=Volatile.Read(ref accepted);
    var start=runtime.StartLiveLaneAsync(MarketDataLane.OptionsGlbx,new string('x',32),"127.0.0.1",port,["127.0.0.1:1"],[new(["ESZ6"],"trades","raw_symbol")],[new(42,"ES20261218C6000","ESZ6",true,null,false,true)],true,TimeSpan.FromSeconds(2),cancel.Token);
    var deadline=System.Diagnostics.Stopwatch.StartNew();
    while(Volatile.Read(ref accepted)==before){if(deadline.Elapsed>TimeSpan.FromSeconds(2))throw new Exception("No native SDK connection.");await Task.Yield();}
    cancel.Cancel();
    try{await start;throw new Exception("Cancelled start unexpectedly succeeded.");}catch(OperationCanceledException){}
    await runtime.StopAndDisposeAsync(TimeSpan.FromSeconds(2));
    while(Volatile.Read(ref closed)!=Volatile.Read(ref accepted)){if(deadline.Elapsed>TimeSpan.FromSeconds(3))throw new Exception("Native socket survived disposal.");await Task.Yield();}
}
stop.Cancel();listener.Stop();await server;await Task.WhenAll(clients);
Console.WriteLine($"PASS: managed live DLL configure/status/hot-read + 100 real SDK start/cancel/dispose cycles; accepted={accepted}, closed={closed}.");

// Happy path crosses the optimized DLL with real SDK protocol and copied DBN records.
if(args.Length>1){
    var wire=await File.ReadAllBytesAsync(args[1]);
    var healthy=new TcpListener(IPAddress.Loopback,0);healthy.Start();var healthyPort=(ushort)((IPEndPoint)healthy.LocalEndpoint).Port;
    using var healthyStop=new CancellationTokenSource();var peers=new List<Task>();
    var healthyServer=Task.Run(async()=>{try{while(true){var socket=await healthy.AcceptTcpClientAsync(healthyStop.Token);peers.Add(Task.Run(async()=>{using(socket){var stream=socket.GetStream();using var reader=new StreamReader(stream,System.Text.Encoding.UTF8,false,1024,true);await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray());var auth=await reader.ReadLineAsync();if(auth is null)return;await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray());var sub=await reader.ReadLineAsync();var begin=await reader.ReadLineAsync();if(sub is null||begin!="start_session")throw new Exception("Invalid SDK subscription setup.");await stream.WriteAsync(wire);if(await reader.ReadLineAsync() is not null)throw new Exception("Expected native socket EOF.");}}));}}catch(OperationCanceledException){}});
    await using(var runtime=new NativeMarketDataRuntime(path,2)){
        runtime.ConfigureLive(new(2026,10,26),Guid.NewGuid(),DateTimeOffset.UnixEpoch,false,16);
        Guid previous=Guid.Empty;
        for(var cycle=0;cycle<11;cycle++){
            if(cycle==0)await runtime.StartLiveLaneAsync(MarketDataLane.OptionsGlbx,new string('x',32),"127.0.0.1",healthyPort,["127.0.0.1:1"],[new(["ESZ6"],"trades","raw_symbol")],[new(42,"ES20261218C6000","ESZ6",true,null,false,true)],true,TimeSpan.FromSeconds(2));
            else await runtime.ResetLiveLaneAsync(MarketDataLane.OptionsGlbx,TimeSpan.FromSeconds(2));
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while((await runtime.WaitForLiveChangesAsync(deadline.Token)&8)==0){}
            while(runtime.ReadLiveObservation(MarketDataLane.OptionsGlbx,"ES20261218C6000",true).Flags==0)await runtime.WaitForLiveChangesAsync(deadline.Token);
            var status=runtime.ReadLiveStatus(MarketDataLane.OptionsGlbx);var tick=runtime.ReadLiveObservation(MarketDataLane.OptionsGlbx,"ES20261218C6000");
            if(status.Phase!=3||tick.Flags!=1||tick.Price!=6_000_250_000_000||tick.Quantity!=4||tick.EventTicks!=14||tick.ReceiveTicks!=15||tick.Generation==previous||tick.Generation!=status.Generation)throw new Exception("Wrong managed hot observation/generation.");previous=tick.Generation;
            var diagnostics=runtime.DrainLiveDiagnostics();if(diagnostics.Length!=2||!diagnostics.Any(d=>d.Message=="complete test warning"))throw new Exception("Missing full provider diagnostics.");
            var events=new LiveMarketEvent[64];var count=runtime.DrainLiveEvents(events);if(count!=2||events[0].Kind!=2||events[0].Instrument!=42||events[1].Kind!=1)throw new Exception("Authoritative contribution missing/duplicated.");
        }
    }
    var epoch=new NativeMarketDataRuntime(path,2);epoch.ConfigureLive(new(2026,10,26),Guid.NewGuid(),DateTimeOffset.UnixEpoch,false,16);
    var reader=new TomasAI.IFM.Application.MarketData.Rust.Api.NativeFuturesPriceReader(epoch,MarketDataLane.FuturesGlbx,"ES20261218",new(2026,10,26));
    var baseline=new TomasAI.IFM.Framework.MarketData.DataBento.LastPrice.DatabentoLastPriceStore(new(2026,10,26),8);baseline.RegisterContract("ES20261218",TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures);var baselineReader=baseline.GetFuturesReader("ES20261218",new(2026,10,26));
    if(reader.TryGetLastTrade(out _))throw new Exception("Reader construction started a subscription.");
    for(var cycle=0;cycle<11;cycle++){
        if(cycle==0)await epoch.StartLiveLaneAsync(MarketDataLane.FuturesGlbx,new string('x',32),"127.0.0.1",healthyPort,["127.0.0.1:1"],[new(["ESZ6"],"trades","raw_symbol")],[new(42,"ES20261218","ESZ6",false,null,true,true)],true,TimeSpan.FromSeconds(2));else await epoch.ResetLiveLaneAsync(MarketDataLane.FuturesGlbx,TimeSpan.FromSeconds(2));
        using var ready=new CancellationTokenSource(TimeSpan.FromSeconds(2));while(!reader.TryGetLastQuote(out _))await epoch.WaitForLiveChangesAsync(ready.Token);
        baseline.ResetContracts(["ES20261218"]);baseline.TryUpdateTrade(new("ES20261218",new(2026,10,26),6000.25m,4,7,DateTimeOffset.UnixEpoch.AddTicks(14),DateTimeOffset.UnixEpoch.AddTicks(15)));baseline.TryUpdateQuote(new("ES20261218",new(2026,10,26),6000m,123,7,6000.25m,456,8,9,DateTimeOffset.UnixEpoch.AddTicks(16),DateTimeOffset.UnixEpoch.AddTicks(17)){LocalReceivedAtUtc=DateTimeOffset.UnixEpoch});
        if(!reader.TryGetLastTrade(out var actualTrade)||!baselineReader.TryGetLastTrade(out var expectedTrade)||actualTrade!=expectedTrade)throw new Exception("Stable trade reader differs from current C#.");if(!reader.TryGetLastQuote(out var actualQuote)||!baselineReader.TryGetLastQuote(out var expectedQuote)||actualQuote!=expectedQuote)throw new Exception("Stable quote reader differs from current C#.");epoch.DrainLiveDiagnostics();
        var publications=new LivePublication[16];var publicationCount=epoch.DrainLivePublications(publications);
        if(publicationCount!=2||publications[0].Update!=3||publications[1].Update!=1)throw new Exception("Captured publication contribution missing or reordered.");
        if(!publications[0].TryGetMarketPrice("ES20261218",TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,out var tradeState)
            ||!publications[1].TryGetMarketPrice("ES20261218",TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,out var quoteState))throw new Exception("Native publication state absent.");
        if(tradeState.Quote is not null||tradeState.Trade?.LastPrice!=expectedTrade.Price||tradeState.Trade?.SourceSequence!=expectedTrade.SourceSequence
            ||quoteState.Quote?.BidPrice!=expectedQuote.BidPrice||quoteState.Quote?.AskPrice!=expectedQuote.AskPrice||quoteState.Quote?.BidCount!=expectedQuote.BidCount
            ||quoteState.Quote?.AskCount!=expectedQuote.AskCount||quoteState.Quote?.EventTimestamp!=expectedQuote.EventTimestamp||quoteState.Trade!=tradeState.Trade
            ||quoteState.ValueDate!=new DateOnly(2026,10,26)||tradeState.Trade?.TradeOrdinal!=1)throw new Exception("Captured publication differs from C# values or includes a later quote in earlier state.");
        if(!publications[0].TryGetVwap(out var vwap)||vwap is null||vwap.CumulativePriceVolume!=24001m||vwap.CumulativeVolume!=4||vwap.LastPrice!=6000.25m
            ||vwap.EligibleTradeCount!=1||vwap.LastTradeOrdinal!=1||vwap.LastTradeSourceSequence!=7||vwap.IsValid||vwap.IsReplayComplete
            ||(int)vwap.InvalidReason!=5)throw new Exception("Native VWAP coefficient/scale or incomplete-state delivery differs from C# source semantics.");
        var currentState=epoch.ReadLiveState(MarketDataLane.FuturesGlbx,42,3);
        if(currentState.Source.Kind!=0||!currentState.TryGetMarketPrice("ES20261218",TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,out var currentPrice)
            ||currentPrice!=quoteState)throw new Exception("Non-consuming native derived-state query differs from final captured publication.");
    }
    await epoch.DisposeAsync();baseline.Invalidate();if(reader.TryGetLastTrade(out _)||reader.TryGetLastQuote(out _)||baselineReader.TryGetLastTrade(out _))throw new Exception("Retired reader still returned values.");
    Console.WriteLine("PASS: stable public futures reader matches unchanged C# trade/quote snapshots across 10 resets, including order counts, exact timestamps, ValueDate and retirement.");
    healthyStop.Cancel();healthy.Stop();await healthyServer;await Task.WhenAll(peers);
    Console.WriteLine("PASS: managed real SDK start + 10 resets, event-driven notifications, exact source events/hot prices, complete warnings and independent delivery.");
}
