using System.Buffers;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Matched durable output boundary; the pre-v2 Regime journal remains the unchanged frozen baseline.</summary>
[MemoryDiagnoser]
public class WorkflowPublicationReplayBenchmarks
{
    const int Batch=8;
    [Params("Small","Representative")]public string Payload{get;set;}="Small";
    [Params("Capture","AcceptedReplay")]public string Mode{get;set;}="Capture";
    WorkflowRegimeFunctionFailures before=null!;WorkflowPublicationJournal after=null!;
    WorkflowStrategyStateUpdatedEvent[] source=[];FailRegimeDiscoveryCommand[] commands=[];
    string directory="";
    [IterationSetup]
    public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"ifm-workflow-v2-bdn-"+Guid.NewGuid().ToString("N"));
        before=new(Path.Combine(directory,"before"));after=new(Path.Combine(directory,"after"));
        source=Enumerable.Range(1,Batch).Select(i=>WorkflowDispatchDeliveryFixture.RegimeFunctionInput(i,Payload=="Representative")).ToArray();
        commands=source.Select(s=>new FailRegimeDiscoveryCommand{CommandId=Guid.NewGuid(),EntityId=s.EntityId,WorkflowId=s.WorkflowId,InputWorkflowRevision=s.WorkflowRevision,
            FailedAtUtc=s.State.UpdatedAtUtc,SourceEventId=s.Id,Subject=new(ActorType.Command,FailRegimeDiscoveryCommand.Actor,FailRegimeDiscoveryCommand.Verb,s.EntityId.Format())}).ToArray();
        if(Mode=="AcceptedReplay")for(var i=0;i<Batch;i++)
        {
            before.Prepare(LegacyKey(source[i]),FrozenLegacyRequestHash.Compute(source[i]),()=>commands[i]);before.MarkSent(LegacyKey(source[i]));
            var key=WorkflowPublicationDelivery.Key(source[i].State);after.Prepare(key,WorkflowPublicationDelivery.Digest(source[i].State),source[i].State,Guid.Empty,commands[i]);after.Confirm(key,WorkflowPublicationDisposition.Accepted);
        }
    }
    [Benchmark(Baseline=true,OperationsPerInvoke=Batch)]
    public int BeforeFrozenLegacyJournal()
    {
        var count=0;
        for(var i=0;i<Batch;i++)
        {
            var key=LegacyKey(source[i]);var hash=FrozenLegacyRequestHash.Compute(source[i]);
            var result=before.Find(key)??before.Prepare(key,hash,()=>commands[i]);
            if(result.InputHash!=hash)throw new InvalidDataException("Legacy source mismatch");
            if(!result.Sent)before.MarkSent(result.Key);count++;
        }
        return count;
    }
    [Benchmark(OperationsPerInvoke=Batch)]
    public int AfterCanonicalOutputJournal()
    {
        var count=0;
        for(var i=0;i<Batch;i++)
        {
            var key=WorkflowPublicationDelivery.Key(source[i].State);var hash=WorkflowPublicationDelivery.Digest(source[i].State);
            var result=after.Find(key)??after.Prepare(key,hash,source[i].State,Guid.Empty,commands[i]);
            if(result.IntentHash!=hash)throw new InvalidDataException("Canonical source mismatch");
            if(result.Disposition==WorkflowPublicationDisposition.Pending)after.Confirm(key,WorkflowPublicationDisposition.Accepted);count++;
        }
        return count;
    }
    static Guid LegacyKey(WorkflowStrategyStateUpdatedEvent source)=>new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{source.State.WorkflowId}|{source.State.CurrentStage}|{source.State.WorkflowRevision}")).AsSpan(0,16));
    [IterationCleanup]
    public void Cleanup(){before.Dispose();after.Dispose();Directory.Delete(directory,true);}

    // Exact pre-v2 pooled hashing algorithm captured in baseline/source.zip. The
    // baseline deliberately hashes the full source; the new boundary hashes accepted intent.
    sealed class FrozenLegacyRequestHash:IBufferWriter<byte>,IDisposable
    {
        readonly IncrementalHash digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer=ArrayPool<byte>.Shared.Rent(16384);
        public static string Compute<T>(T request){using var writer=new FrozenLegacyRequestHash();MessagePackSerializer.Serialize(writer,request);return Convert.ToHexString(writer.digest.GetHashAndReset());}
        public void Advance(int count){if(count<0||count>buffer.Length)throw new ArgumentOutOfRangeException(nameof(count));digest.AppendData(buffer.AsSpan(0,count));}
        public Memory<byte> GetMemory(int sizeHint=0){Ensure(sizeHint);return buffer;}
        public Span<byte> GetSpan(int sizeHint=0){Ensure(sizeHint);return buffer;}
        void Ensure(int sizeHint){if(sizeHint<0)throw new ArgumentOutOfRangeException(nameof(sizeHint));if(sizeHint<=buffer.Length)return;var next=ArrayPool<byte>.Shared.Rent(sizeHint);ArrayPool<byte>.Shared.Return(buffer,clearArray:true);buffer=next;}
        public void Dispose(){if(buffer.Length==0)return;ArrayPool<byte>.Shared.Return(buffer,clearArray:true);buffer=[];digest.Dispose();}
    }
}
