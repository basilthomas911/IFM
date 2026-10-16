using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;

static class BridgeProbe
{
    public static async Task RunAsync(string output) {
        const int count=1000,iterations=1000;
        var rows=Enumerable.Range(0,count).Select(i=>new ContractEditorRow(i.ToString(),new(){["kind"]="option"}){
            Data=BridgeDocument.Encode(new{contractId=$"ES-C-{5000+i}",strike=5000+i,bid=2.1,ask=2.3,delta=.16})}).ToArray();
        var result=new ContractEditorResult("option-contract",rows,[]);
        var gateway=new ContractEditorGateway(_=>Task.FromResult(result));
        gateway.Submit(new(1,Guid.NewGuid(),"option-contract","load",1,null,[]));await gateway.DrainAsync();
        var document=new{contracts=Enumerable.Range(0,count).Select(i=>new{contractId=$"ES-C-{5000+i}",strike=5000+i,bid=2.1,ask=2.3,delta=.16}).ToArray()};
        for(var i=0;i<100;i++){_=gateway.WireSnapshot();_=BridgeDocument.Encode(document,true);}
        // Controlled benchmark boundary only. The application never forces collections.
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        long allocated=GC.GetTotalAllocatedBytes(true),contentions=Monitor.LockContentionCount;
        var pause=GC.GetTotalPauseDuration();var collections=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
        var times=new double[iterations];long maxPending=0;double refreshUs=0,encodeUs=0;var clock=Stopwatch.StartNew();
        for(var i=0;i<iterations;i++){
            var start=Stopwatch.GetTimestamp();_=gateway.WireSnapshot();times[i]=Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            for(var read=1;read<25;read++)_=gateway.WireSnapshot();
            var refreshStart=Stopwatch.GetTimestamp();
            if(!gateway.TryRefresh("option-contract",()=>Task.FromResult(result)))throw new InvalidOperationException("Refresh rejected");
            await gateway.DrainAsync();_=gateway.WireSnapshot();refreshUs+=Stopwatch.GetElapsedTime(refreshStart).TotalMicroseconds;
            var encodeStart=Stopwatch.GetTimestamp();_=BridgeDocument.Encode(document,true);encodeUs+=Stopwatch.GetElapsedTime(encodeStart).TotalMicroseconds;
            maxPending=Math.Max(maxPending,ThreadPool.PendingWorkItemCount);
        }
        clock.Stop();Array.Sort(times);
using var process=Process.GetCurrentProcess();process.Refresh();
        var metrics=new{serverGc=GCSettings.IsServerGC,rows=count,iterations,readsPerIteration=25,elapsedSeconds=clock.Elapsed.TotalSeconds,
            allocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,collections=Enumerable.Range(0,3).Select(i=>GC.CollectionCount(i)-collections[i]).ToArray(),
            gcPauseMs=(GC.GetTotalPauseDuration()-pause).TotalMilliseconds,lockContentions=Monitor.LockContentionCount-contentions,
            maxPendingThreadPoolItems=maxPending,managedHeapBytes=GC.GetTotalMemory(false),privateBytes=process.PrivateMemorySize64,
            workingSetBytes=process.WorkingSet64,peakWorkingSetBytes=process.PeakWorkingSet64,processThreads=process.Threads.Count,
            averageRefreshUs=refreshUs/iterations,averageEncodeUs=encodeUs/iterations,readP50Us=times[iterations/2],readP95Us=times[(int)(iterations*.95)],readP99Us=times[(int)(iterations*.99)]};
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(metrics,new JsonSerializerOptions{WriteIndented=true}));
    }
}
