using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

var date=new DateOnly(2026,9,8);var generation=Guid.NewGuid();var admissions=new DatasetWorkerAdmissionRegistry();
admissions.Admit(new("GLBX.MDP3",date,Guid.NewGuid(),generation,1));
var discovery=new BlockedDiscovery();var time=new FixedTime(new(2026,9,8,16,0,0,TimeSpan.Zero));
await using var registry=new NativeRecoveringOptionLeaseRegistry(discovery,admissions,timeProvider:time);
using var firstCancellation=new CancellationTokenSource();
var request=new CompositionDiscoveryRequest(Guid.NewGuid(),generation,date,date.AddDays(20),time.GetUtcNow().AddMinutes(1),[],true,null!,null!,null!);
var first=registry.AcquireAsync(new("fixture","owner1","contract1"),"contract1",request,firstCancellation.Token);
await discovery.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
if(discovery.Request?.DeadlineUtc!=time.GetUtcNow().AddSeconds(60))throw new InvalidDataException("Lease deadline used machine time instead of supplied API clock.");
using var secondCancellation=new CancellationTokenSource();
var second=registry.AcquireAsync(new("fixture","owner2","contract2"),"contract2",request with{LeaseId=Guid.NewGuid()},secondCancellation.Token);
secondCancellation.Cancel();
try{await second.WaitAsync(TimeSpan.FromSeconds(2));throw new InvalidDataException("A caller could not cancel admission wait.");}catch(OperationCanceledException){}
if(discovery.Calls!=1)throw new InvalidDataException("Canceled admission created a second provider acquisition.");
firstCancellation.Cancel();
try{await first.WaitAsync(TimeSpan.FromSeconds(2));throw new InvalidDataException("Caller cancellation did not reach discovery.");}catch(OperationCanceledException){}
await discovery.Exited.Task.WaitAsync(TimeSpan.FromSeconds(2));
var activated=false;
await registry.AdoptRestoredLeasesAsync([],default,()=>activated=true);
if(!activated)throw new InvalidDataException("Restored ownership activation was not performed under the registry transition.");
await registry.DisposeAsync();
Console.WriteLine("PASS native lease cancellation: API clock reaches discovery; canceled serial wait starts no acquisition; caller cancellation joins actual discovery; disposal completes without waiting for detached lease work.");
sealed class FixedTime(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
sealed class BlockedDiscovery:IRecoverableOptionDiscovery
{
    public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CompositionDiscoveryRequest? Request;public int Calls;
    public async Task<CompositionDiscoveryResult> AcquireAsync(CompositionDiscoveryRequest request,CancellationToken token,bool refreshAutomatically=true,bool renewAutomatically=false)
    {
        Calls++;Request=request;Entered.TrySetResult();
        try{await Task.Delay(Timeout.InfiniteTimeSpan,token);throw new InvalidOperationException("Unreachable.");}
        finally{Exited.TrySetResult();}
    }
    public Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRequest lease,CancellationToken token)=>throw new InvalidOperationException("Unexpected release.");
    public void ForgetLease(Guid leaseId){}
    public bool IsLeaseCurrent(Guid leaseId)=>false;
}
