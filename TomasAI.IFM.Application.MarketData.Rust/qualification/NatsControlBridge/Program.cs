using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.MarketData.Contracts;

// Dedicated random subjects only. This never starts/stops an IFM server or feed.
using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
await using var manager=new NatsConnectionManager();
await using var connection=new MarketDataHostConnection(manager,"nats://127.0.0.1:4222","nativequalification"+Guid.NewGuid().ToString("N"));
var api=DispatchProxy.Create<IMarketDataApi,ApiProbe>();var probe=(ApiProbe)(object)api;
using var service=new NativeMarketDataNatsService(api,connection,NullLogger<NativeMarketDataNatsService>.Instance,TimeProvider.System);
await service.StartAsync(deadline.Token);await service.Ready.WaitAsync(deadline.Token);
if(!await connection.PostAsync<bool>("ready",new{},deadline.Token))throw new InvalidDataException("Native NATS subscriptions were not ready.");
var method=typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.ReadTreasuryCurvesAsync))!;
var call=new MarketDataApiCall(MarketDataApiClient.Signature(method),[MarketDataHostConnection.Encode(new DateOnly(2026,10,25)),MarketDataHostConnection.Encode(new DateOnly(2026,10,26)),[]]);
var tasks=Enumerable.Range(0,8).Select(_=>connection.PostAsync<TreasuryCurveSnapshot[]>("api/ReadTreasuryCurvesAsync",call,deadline.Token)).ToArray();
await probe.Entered.Task.WaitAsync(deadline.Token);
// Eight occupied control handlers must not hold observation or stop delivery.
if(!await connection.PostAsync<bool>("ready",new{},deadline.Token))throw new InvalidDataException("Observation lane blocked behind control.");
var stopMethod=typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.StopAsync))!;
var stop=new MarketDataApiCall(MarketDataApiClient.Signature(stopMethod),[MarketDataHostConnection.Encode(new DateOnly(2026,10,26))]);
await connection.PostAsync<bool>("api/StopAsync",stop,deadline.Token);
if(probe.Stops!=1)throw new InvalidDataException("Emergency lane did not bypass occupied control.");
probe.Release.SetResult();await Task.WhenAll(tasks).WaitAsync(deadline.Token);
var operation=Guid.NewGuid();await connection.PostAsync<TreasuryCurveSnapshot[]>("api/ReadTreasuryCurvesAsync",call,deadline.Token,operation);
var calls=probe.Calls;await connection.PostAsync<TreasuryCurveSnapshot[]>("api/ReadTreasuryCurvesAsync",call,deadline.Token,operation);
if(probe.Calls!=calls)throw new InvalidDataException("Duplicate operation executed twice.");
try{await connection.PostAsync<bool>("api/StopAsync",call,deadline.Token);throw new InvalidDataException("Mismatched signature was dispatched.");}catch(InvalidOperationException){}
var changed=call with{Arguments=[MarketDataHostConnection.Encode(new DateOnly(2026,10,24)),call.Arguments[1],[]]};
try{await connection.PostAsync<TreasuryCurveSnapshot[]>("api/ReadTreasuryCurvesAsync",changed,deadline.Token,operation);throw new InvalidDataException("Operation input conflict was accepted.");}catch(InvalidOperationException){}
await service.StopAsync(deadline.Token);
Console.WriteLine("PASS native Host NATS: existing API framing; all subscribers ready; observation and emergency bypass eight occupied control handlers; exact duplicate deduplication; signature/input conflicts rejected.");

public class ApiProbe:DispatchProxy
{
    public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls,Stops;
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        if(method!.Name==nameof(IMarketDataApi.ReadTreasuryCurvesAsync))return Read();
        if(method.Name==nameof(IMarketDataApi.StopAsync)){Interlocked.Increment(ref Stops);return Task.CompletedTask;}
        throw new InvalidDataException("Unexpected API operation: "+method.Name);
    }
    async Task<TreasuryCurveSnapshot[]> Read(){if(Interlocked.Increment(ref Calls)==8)Entered.TrySetResult();await Release.Task;return [];}
}
