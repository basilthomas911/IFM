using System.Diagnostics;
using System.Globalization;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

// Explicit opt-in real-provider transport qualification, never part of the
// offline runner. This opens one ES/VX futures subscription, resets that native
// owner once, and joins it. It sends no order and publishes no application event.
// The caller supplies the business date/time: no local ValueDate policy exists.
if(args.Length is <4 or >6)throw new ArgumentException("Expected native DLL, API ValueDate (yyyy-MM-dd), API UTC timestamp, exact raw symbol, optional --vx and optional replay UTC timestamp.");
var valueDate=DateOnly.ParseExact(args[1],"yyyy-MM-dd",CultureInfo.InvariantCulture);
var at=DateTimeOffset.Parse(args[2],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
if(at.Offset!=TimeSpan.Zero)throw new ArgumentException("API timestamp must be UTC.");
var vx=args.Length>4&&args[4]=="--vx";var lane=vx?MarketDataLane.FuturesXcbf:MarketDataLane.FuturesGlbx;var dataset=vx?"XCBF.PITCH":"GLBX.MDP3";
long? replay=args.Length>5?checked((DateTimeOffset.Parse(args[5],CultureInfo.InvariantCulture).UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks)*100):null;
var symbol=args[3];if(symbol.Length>16||!symbol.StartsWith(vx?"VX":"ES",StringComparison.Ordinal)||symbol.Any(char.IsWhiteSpace))throw new ArgumentException("An exact ES/VX futures raw symbol is required.");
var key=Environment.GetEnvironmentVariable("DATABENTO_API_KEY")??throw new InvalidOperationException("DATABENTO_API_KEY is required.");
var budget=TimeSpan.FromMinutes(2);var dns=new[]{"1.1.1.1:53","8.8.8.8:53"};
await using var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]),2);
runtime.ConfigureLive(valueDate,Guid.NewGuid(),at,true,16);
runtime.ConfigureHistory("https://hist.databento.com/",key,dns);
using var cancel=new CancellationTokenSource();
var counters=new long[7];var observations=0L;
var contract="";
LiveLaneStatus? previous=null;
var receiver=Task.Run(async()=>
{
    var buffer=new LiveMarketEvent[64];
    try{while(true)
    {
        await runtime.WaitForLiveChangesAsync(cancel.Token);
        int count;do{count=runtime.DrainLiveEvents(buffer);for(var i=0;i<count;i++)Interlocked.Increment(ref counters[buffer[i].Kind]);}while(count==buffer.Length);
        foreach(var diagnostic in runtime.DrainLiveDiagnostics())
            Console.WriteLine($"provider diagnostic lane={diagnostic.Lane} kind={diagnostic.Kind} code={diagnostic.Code}: {diagnostic.Message.Replace(key,"[redacted]",StringComparison.Ordinal)}");
        foreach(var warning in runtime.DrainHistoricalWarnings())
            Console.WriteLine($"provider notice endpoint={warning.Endpoint} status={warning.Status}: {warning.Text.Replace(key,"[redacted]",StringComparison.Ordinal)}");
        var status=runtime.ReadLiveStatus(lane);if(status!=previous){previous=status;if(status.Error is{} reason)Console.WriteLine($"lane phase={status.Phase} attempt={status.Attempt}: {reason.Replace(key,"[redacted]",StringComparison.Ordinal)}");}
        if(Volatile.Read(ref contract) is{Length:>0} id&&(runtime.ReadLiveObservation(lane,id).Flags!=0||runtime.ReadLiveObservation(lane,id,true).Flags!=0))Interlocked.Increment(ref observations);
    }}catch(OperationCanceledException)when(cancel.IsCancellationRequested){}
});
try
{
    var started=Stopwatch.GetTimestamp();
    using var snapshot=await runtime.AcquireExactDefinitionSnapshotAsync(dataset,[symbol],budget);
    var definition=Enumerable.Range(0,snapshot.Count).Select(snapshot.ReadDefinition).Where(x=>x.Symbol==symbol&&!x.Deleted).OrderByDescending(x=>x.ReceivedNanoseconds).FirstOrDefault()
        ??throw new InvalidDataException("Exact provider definition unavailable.");
    Volatile.Write(ref contract,definition.CanonicalId??throw new InvalidDataException("Definition lacks a qualified canonical identity."));
    Console.WriteLine($"Exact definition acquired in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}ms; symbol={symbol}; contract={contract}; rows={snapshot.Count}.");
    var providerId=definition.Instrument.ToString(CultureInfo.InvariantCulture);
    var subscriptions=new[]{new NativeSubscription([providerId],"trades","instrument_id",replay),new NativeSubscription([providerId],"mbp-1","instrument_id"),new NativeSubscription([providerId],"statistics","instrument_id",replay)};
    var registrations=new[]{new NativeRegistration(definition.Instrument,contract,providerId,false,definition.Tick,!vx,true,definition.Publisher)};
    started=Stopwatch.GetTimestamp();
    await runtime.StartLiveLaneAsync(lane,key,vx?"xcbf-pitch.lsg.databento.com":"glbx-mdp3.lsg.databento.com",13000,dns,subscriptions,registrations,true,budget);
    var before=runtime.ReadLiveStatus(lane);
    if(before.Phase!=3)throw new InvalidDataException("Real provider lane did not qualify.");
    Console.WriteLine($"Live source qualified in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}ms.");
    started=Stopwatch.GetTimestamp();await runtime.ResetLiveLaneAsync(lane,budget);
    var after=runtime.ReadLiveStatus(lane);
    if(after.Phase!=3||after.Generation==before.Generation)throw new InvalidDataException("Real provider replacement did not qualify a new generation.");
    Console.WriteLine($"Real provider reset qualified in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}ms; changed generation; same process.");
}
catch(Exception error)
{
    Console.WriteLine($"FAIL live-provider qualification: {error.GetType().Name}: {error.Message.Replace(key,"[redacted]",StringComparison.Ordinal)}");
    Console.WriteLine(error.StackTrace);
    Environment.ExitCode=1;
}
finally
{
    cancel.Cancel();await receiver.WaitAsync(budget);await runtime.StopAndDisposeAsync(budget);
}
if(Environment.ExitCode==0)Console.WriteLine($"PASS real-provider native catalog/DNS/auth/subscription/reset/join; observed_signals={observations}; event_counts={string.Join(',',counters)}. This is not Host/actor/UI acceptance.");
