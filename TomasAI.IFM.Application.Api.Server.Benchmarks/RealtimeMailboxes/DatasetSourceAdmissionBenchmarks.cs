using BenchmarkDotNet.Attributes;
using MessagePack;
using System.Security.Cryptography;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Premature-watermark bookkeeping versus confirmed-ownership bookkeeping, a stronger guarantee.
/// Capture transport/storage cost is measured separately; no durable IO is included here.</summary>
[MemoryDiagnoser]
public class DatasetSourceAdmissionBenchmarks
{
    readonly DatasetWorkerAdmission identity=new("GLBX.MDP3",new DateOnly(2026,11,20),Guid.NewGuid(),Guid.NewGuid(),1);
    DatasetWorkerAdmissionRegistry before=null!,after=null!;
    DatasetPublicationEnvelope source=null!;
    long beforeSequence,afterSequence;
    [Params(128,16384)] public int PayloadBytes{get;set;}
    [GlobalSetup]
    public void Setup()
    {
        before=new();after=new();before.Admit(identity);after.Admit(identity);
        source=new(){Dataset=identity.Dataset,ValueDate=identity.ValueDate,WorkerInstanceId=identity.WorkerInstanceId,GenerationId=identity.GenerationId,ManifestRevision=1,PublicationSequence=1,Kind=DatasetPublicationKind.Trade,Payload=new byte[PayloadBytes]};
    }
    [Benchmark(Baseline=true)]
    public bool BeforePrematureWatermark()=>before.TryAccept(identity,++beforeSequence,out _);
    [Benchmark]
    public async ValueTask<bool> AfterConfirmedOwnership()
    {
        var frame=source with{PublicationSequence=++afterSequence};
        var hash=Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(frame)));
        using var pending=await after.BeginPublicationAsync(identity,frame.PublicationSequence,hash);
        if(pending is null)return false;
        // Actual durable capture must succeed here; no fake IO is timed or claimed by this microbenchmark.
        pending.ConfirmCapture();return true;
    }
}
