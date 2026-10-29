using System.Collections.Concurrent;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Host;

var notices=new ConcurrentQueue<MarketDataDatasetStatus>();
var recovery=new NativeHostRecovery(TimeProvider.System,notices.Enqueue);
var current=new MarketDataDatasetStatus("Options","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),Environment.ProcessId,true,false,"Recovering","fixture",new(2026,10,26));
MarketDataRecoveryRequest Request(TimeSpan budget)=>new(Guid.NewGuid(),current.Role,current.Dataset,current.WorkerInstanceId,current.GenerationId,current.ValueDate!.Value,"injected",DeadlineUtc:DateTimeOffset.UtcNow+budget);
var attempts=0;
var happy=await recovery.RecoverAsync(current,Request(TimeSpan.FromSeconds(2)),(_,_)=>++attempts==1?Task.FromException<bool>(new IOException("transient fixture")):Task.FromResult(true));
if(!happy.Recovered||attempts!=2||notices.Last() is not{Lifecycle:"Healthy",Error:null,RecoveryDeadlineUtc:null})throw new InvalidDataException("Retry did not recover or clear its indicator.");
var started=Stopwatch.GetTimestamp();attempts=0;
var failed=await recovery.RecoverAsync(current,Request(TimeSpan.FromMilliseconds(650)),(_,_)=>{attempts++;return Task.FromException<bool>(new IOException("persistent fixture"));});
var elapsed=Stopwatch.GetElapsedTime(started);
if(failed.Recovered||!failed.CleanupConfirmed||attempts<3||elapsed<TimeSpan.FromMilliseconds(600)||elapsed>TimeSpan.FromSeconds(2)||notices.Last().Lifecycle!="StoppedError")throw new InvalidDataException("A failed step ended early, renewed its deadline or missed terminal notification.");
var hold=new TaskCompletionSource<bool>();attempts=0;
var request=Request(TimeSpan.FromMilliseconds(200));
var blocked=await recovery.RecoverAsync(current,request,(_,_)=>{attempts++;return hold.Task;});
if(blocked.Recovered||blocked.CleanupConfirmed||attempts!=1)throw new InvalidDataException("Blocked callback falsely joined.");
var joined=await recovery.RecoverAsync(current,Request(TimeSpan.FromSeconds(1)),(_,_)=>{attempts++;return Task.FromResult(true);});
if(joined!=blocked||attempts!=1)throw new InvalidDataException("Terminal timeout spawned another blocked callback.");
hold.SetResult(true);
var retried=await recovery.RecoverAsync(current,Request(TimeSpan.FromSeconds(1)),(_,_)=>Task.FromResult(true));
if(!retried.Recovered)throw new InvalidDataException("Joined late callback left a permanent recovery gate.");
var stale=await recovery.RecoverAsync(current,Request(TimeSpan.FromSeconds(1)) with{ExpectedGenerationId=Guid.NewGuid()},(_,_)=>throw new Exception("Stale recovery invoked reset."));
if(stale.GenerationId!=current.GenerationId)throw new InvalidDataException("Stale reset replaced a newer owner.");
Console.WriteLine("PASS Host recovery: retry then success/indicator reset; full original deadline despite repeated failures; non-cooperative timeout with terminal notification; one owned callback; late join releases gate; stale owner fenced.");

var concurrentHold=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var concurrentRequest=Request(TimeSpan.FromSeconds(2));
var concurrent=recovery.RecoverAsync(current,concurrentRequest,(_,_)=>concurrentHold.Task);
if(!recovery.TryJoin(concurrentRequest with{ExpectedGenerationId=Guid.NewGuid()},out var owned)||!ReferenceEquals(concurrent,owned))
    throw new InvalidDataException("An observer missed an owned episode after generation rotation.");
if(recovery.TryJoin(concurrentRequest with{ValueDate=current.ValueDate!.Value.AddDays(1)},out _))throw new InvalidDataException("A different date joined stale recovery.");
concurrentHold.SetResult(true);await concurrent;
var replacement=current with{WorkerInstanceId=Guid.NewGuid(),GenerationId=Guid.NewGuid()};
var ownerProof=await recovery.RecoverAsync(current,Request(TimeSpan.FromSeconds(1)),(_,_)=>Task.FromResult(true),restoredStatus:()=>replacement);
if(ownerProof.WorkerInstanceId!=replacement.WorkerInstanceId||ownerProof.GenerationId!=replacement.GenerationId||notices.Last().GenerationId!=replacement.GenerationId)
    throw new InvalidDataException("Whole-owner recovery returned the retired identity.");

if(args.Contains("--production-deadline"))
{
    var never=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var realStart=Stopwatch.GetTimestamp();
    var real=await recovery.RecoverAsync(current,Request(TimeSpan.FromMinutes(2)),(_,_)=>never.Task);
    var realElapsed=Stopwatch.GetElapsedTime(realStart);
    if(real.Recovered||real.CleanupConfirmed||realElapsed<TimeSpan.FromSeconds(119.5)||realElapsed>TimeSpan.FromSeconds(125)||notices.Last().Lifecycle!="StoppedError")
        throw new InvalidDataException("Production deadline stopped early, hung or falsely confirmed cleanup.");
    never.TrySetResult(false);
    Console.WriteLine($"PASS actual Host 120-second deadline: terminal observation at {realElapsed.TotalSeconds:F3}s; unjoined callback not reported cleaned.");
}
