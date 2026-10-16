using BenchmarkDotNet.Attributes;
using TomasAI.IFM.UI.Rust.Bridge.Net;

[MemoryDiagnoser]
public class TransferBenchmarks
{
    [Params(100,1000)] public int Rows {get;set;}
    PublicationTransfer transfer=null!,full=null!;
    byte[] first=[],second=[];
    ulong cursor;
    bool toggle;
    [GlobalSetup] public void Setup(){
        byte[] Snapshot(double bid)=>BridgeDocument.Encode(new { contracts=Enumerable.Range(0,Rows).Select(i=>new { contractId=$"ES-C-{5000+i}",strike=5000+i,bid=i==50?bid:2.1,ask=2.3,delta=.16,volume=1000,openInterest=5000 }).ToArray() });
        first=Snapshot(2.1);second=Snapshot(2.2);transfer=new(incremental:true);full=new();_=full.Read(first,0);cursor=transfer.Read(first,0)!.Value.Sequence;
    }
    [Benchmark(Baseline=true)] public byte[] FullCopy()=>first.ToArray();
    [Benchmark] public byte[] ChangedFull(){toggle=!toggle;var result=full.Read(toggle?second:first,cursor)!.Value;cursor=result.Sequence;return result.Bytes.ToArray();}
    [Benchmark] public object? Unchanged()=>transfer.Read(toggle?second:first,cursor);
    [Benchmark] public byte[] SparseChange(){toggle=!toggle;var result=transfer.Read(toggle?second:first,cursor)!.Value;cursor=result.Sequence;return result.Bytes.ToArray();}
    [GlobalCleanup] public void Cleanup(){transfer.Dispose();full.Dispose();}
}
