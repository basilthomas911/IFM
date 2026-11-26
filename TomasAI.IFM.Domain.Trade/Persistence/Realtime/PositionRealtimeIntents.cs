using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Threading.Channels;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Persistence.Realtime;

[GeneratedMessagePackResolver]
public partial class PositionIntentResolver;

public enum PositionIntentKind : byte { Lifecycle=1,FuturesPlan=2,VerticalPlan=3,ClosedFence=4 }
[Flags] public enum PositionIntentFlags : byte { None=0,OutputCaptured=1,Published=2,Cleaned=4,Completed=8,Retired=16 }
[MessagePackObject]
public sealed record PositionRealtimeIntent
{
    [Key(0)] public Guid Key {get;init;}
    [Key(1)] public PositionIntentKind Kind {get;init;}
    [Key(2)] public string SourceHash {get;init;}="";
    [Key(3)] public StrategyPositionId PositionId {get;init;}
    [Key(4)] public long RouteGeneration {get;init;}
    [Key(5)] public long PositionSequence {get;init;}
    [Key(6)] public byte[] Request {get;init;}=[];
    [Key(7)] public byte[] Output {get;init;}=[];
    [Key(8)] public string? Origin {get;init;}
    [Key(9)] public PositionIntentFlags Flags {get;init;}
    [Key(10)] public long Ordinal {get;init;}
    [Key(11)] public int Version {get;init;}=1;
    [Key(12)] public string? OutputOrigin {get;init;}
    [IgnoreMember] public bool IsCompleted=>(Flags&PositionIntentFlags.Completed)!=0;
}

/// <summary>Single-host bounded typed intent log. Group flush owns input before dispatch; compaction retains every required replay record.</summary>
public sealed class PositionRealtimeIntents : IAsyncDisposable,IDisposable
{
    const int FrameLimit=8*1024*1024,MaximumKeys=100000,MaximumPending=4096;
    readonly long retainedLimit,logLimit;
    readonly string? path;
    readonly FileStream? lease;
    FileStream? log;
    readonly object gate=new();
    readonly Dictionary<Guid,Slot> slots=[];
    readonly Dictionary<Guid,byte[]> committed=[];
    readonly Channel<Write> writes=Channel.CreateBounded<Write>(new BoundedChannelOptions(128){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
    readonly Task writer;
    long retained,ordinal,flushes,compactions;
    int pendingCount;
    readonly Dictionary<StrategyPositionId,(long Generation,long Sequence)> closedFloors=[];
    string? lastFailure;
    bool disposed;
    sealed class Slot {public readonly SemaphoreSlim Gate=new(1);public byte[]? Bytes;public bool Durable;public bool Completed;public PositionIntentKind Kind;public StrategyPositionId Position;public long Generation,Sequence;}
    sealed record Write(Guid Key,byte[] Bytes,TaskCompletionSource Completion);
    public PositionRealtimeIntents(string? directory=null,long maximumRetainedBytes=128L*1024*1024,long maximumLogBytes=512L*1024*1024)
    {
        if(maximumRetainedBytes<1024||maximumLogBytes<maximumRetainedBytes)throw new ArgumentOutOfRangeException(nameof(maximumRetainedBytes));
        retainedLimit=maximumRetainedBytes;logLimit=maximumLogBytes;
        if(directory is not null)
        {
            var root=Path.GetFullPath(directory);AtomicDurableFile.EnsureDirectory(root);path=Path.Combine(root,"intents.pil");
            lease=new(Path.Combine(root,"owner.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            try{Open();RecoverLog(true);}catch{log?.Dispose();lease.Dispose();throw;}
        }
        writer=Task.Run(WriteLoopAsync);
    }
    public bool IsHealthy=>Volatile.Read(ref lastFailure) is null && !writer.IsFaulted;
    public string? LastFailure=>Volatile.Read(ref lastFailure);
    public long RetainedBytes=>Interlocked.Read(ref retained);
    public long GroupFlushes=>Interlocked.Read(ref flushes);
    public long Compactions=>Interlocked.Read(ref compactions);
    public static string Hash<T>(T source)=>Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(source)));
    Slot GetSlot(Guid key)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(slots.TryGetValue(key,out var found))return found;
            if(slots.Count>=MaximumKeys)throw Capacity("Position intent key capacity exhausted.");
            var slot=new Slot();slots.Add(key,slot);return slot;
        }
    }
    IOException Capacity(string message){Volatile.Write(ref lastFailure,message);return new IOException(message);}
    public async ValueTask<PositionRealtimeIntent?> FindAsync(Guid key,string hash,CancellationToken token=default)
    {
        Slot? slot;lock(gate)slots.TryGetValue(key,out slot);if(slot is null)return null;
        await slot.Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(slot.Bytes is null)return null;var result=Decode(slot.Bytes);
            if(result.SourceHash!=hash)throw new InvalidDataException("Position intent identity reused with different accepted input.");
            await CommitAsync(slot,key,slot.Bytes).ConfigureAwait(false);return Decode(slot.Bytes);
        }
        finally{slot.Gate.Release();}
    }
    public async ValueTask<PositionRealtimeIntent> CaptureAsync(Guid key,string hash,Func<PositionRealtimeIntent> capture,CancellationToken token=default)
    {
        var slot=GetSlot(key);await slot.Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(slot.Bytes is null)
            {
                lock(gate)if(pendingCount>=MaximumPending)throw Capacity("Position pending-intent capacity exhausted; retain source.");
                var value=capture() with{Key=key,SourceHash=hash,Ordinal=Interlocked.Increment(ref ordinal)};Validate(value);
                if(value.Kind!=PositionIntentKind.ClosedFence && Retired(value.PositionId,value.RouteGeneration,value.PositionSequence))return value with{Flags=PositionIntentFlags.Completed|PositionIntentFlags.Retired};
                SetCandidate(slot,Encode(value),value);
            }
            else if(Decode(slot.Bytes).SourceHash!=hash)throw new InvalidDataException("Position intent input changed under its original identity.");
            await CommitAsync(slot,key,slot.Bytes).ConfigureAwait(false);return Decode(slot.Bytes);
        }
        finally{slot.Gate.Release();}
    }
    public async ValueTask<PositionRealtimeIntent> AdvanceAsync(Guid key,PositionIntentFlags flags,byte[]? output=null,CancellationToken token=default,string? outputOrigin=null)
    {
        var slot=GetSlot(key);await slot.Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var original=slot.Bytes is {} bytes?Decode(bytes):throw new InvalidDataException("Position intent was not captured.");
            if(output is not null && original.Output.Length>0 && !original.Output.AsSpan().SequenceEqual(output))throw new InvalidDataException("Frozen position output changed.");
            if(outputOrigin is not null && original.OutputOrigin is {} frozenOrigin && frozenOrigin!=outputOrigin)throw new InvalidDataException("Frozen position output origin changed.");
            var updated=original with{Flags=original.Flags|flags,Output=output??original.Output,OutputOrigin=outputOrigin??original.OutputOrigin};Validate(updated);
            if(updated.Flags!=original.Flags || output is not null && original.Output.Length==0)SetCandidate(slot,Encode(updated),updated);
            await CommitAsync(slot,key,slot.Bytes!).ConfigureAwait(false);return Decode(slot.Bytes!);
        }
        finally{slot.Gate.Release();}
    }
    public PositionRealtimeIntent[] Pending(PositionIntentKind kind)
    {
        lock(gate)return slots.Values.Where(s=>s.Bytes is not null && s.Kind==kind && (!s.Completed||!s.Durable)).Select(s=>Decode(s.Bytes!)).OrderBy(x=>x.Ordinal).ToArray();
    }
    public bool IsRetired(StrategyPositionSnapshot position)
    {lock(gate)return Retired(position.Id,position.RouteGeneration,position.PositionSequence);}
    bool Retired(StrategyPositionId position,long generation,long sequence)
    {lock(gate)return closedFloors.TryGetValue(position,out var floor)
        && (floor.Generation>generation || floor.Generation==generation && floor.Sequence>=sequence);}
    void ObserveFence(PositionRealtimeIntent record)
    {
        if(record.Kind!=PositionIntentKind.ClosedFence)return;
        if(!closedFloors.TryGetValue(record.PositionId,out var old)||record.RouteGeneration>old.Generation||record.RouteGeneration==old.Generation&&record.PositionSequence>old.Sequence)
            closedFloors[record.PositionId]=(record.RouteGeneration,record.PositionSequence);
    }
    /// <summary>Caller must supply the authoritative closed position, not an inferred missing route. The durable fence suppresses older replay before compaction can forget it.</summary>
    public async ValueTask RetireClosedAsync(StrategyPositionSnapshot closed,Guid evidenceId,CancellationToken token=default)
    {
        if(closed.IsOpen||closed.RouteGeneration<0||evidenceId==Guid.Empty)throw new ArgumentException("An authoritative closed position/evidence is required.");
        var key=TradePlanContractIdentity.DeterministicId($"position-closed-fence/v1|{closed.Id.Format()}|{closed.RouteGeneration}|{closed.PositionSequence}");
        await CaptureAsync(key,Hash(closed),()=>new(){Kind=PositionIntentKind.ClosedFence,PositionId=closed.Id,RouteGeneration=closed.RouteGeneration,
            PositionSequence=closed.PositionSequence,Request=MessagePackSerializer.Serialize(closed),Output=evidenceId.ToByteArray(),Flags=PositionIntentFlags.Completed|PositionIntentFlags.Retired},token).ConfigureAwait(false);
        Guid[] retired;
        lock(gate)retired=slots.Where(pair=>pair.Value.Bytes is not null && pair.Value.Kind!=PositionIntentKind.ClosedFence
            && Retired(pair.Value.Position,pair.Value.Generation,pair.Value.Sequence) && !pair.Value.Completed).Select(pair=>pair.Key).ToArray();
        foreach(var batch in retired.Chunk(16))await Task.WhenAll(batch.Select(id=>AdvanceAsync(id,PositionIntentFlags.Completed|PositionIntentFlags.Retired,token:token).AsTask())).ConfigureAwait(false);
    }
    void SetCandidate(Slot slot,byte[] bytes,PositionRealtimeIntent value)
    {
        lock(gate)
        {
            if(slot.Bytes is null && !value.IsCompleted && pendingCount>=MaximumPending)throw Capacity("Position pending-intent capacity exhausted; retain source.");
            var updated=retained-(slot.Bytes?.Length??0)+bytes.Length;
            if(updated>retainedLimit)throw Capacity("Position intent replay evidence capacity exhausted; no records were discarded.");
            pendingCount-=(slot.Bytes is not null&&!slot.Completed)?1:0;pendingCount+=value.IsCompleted?0:1;
            retained=updated;slot.Bytes=bytes;slot.Durable=false;slot.Completed=value.IsCompleted;slot.Kind=value.Kind;
            slot.Position=value.PositionId;slot.Generation=value.RouteGeneration;slot.Sequence=value.PositionSequence;
        }
    }
    async ValueTask CommitAsync(Slot slot,Guid key,byte[] bytes)
    {
        if(slot.Durable)return;
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await writes.Writer.WriteAsync(new(key,bytes,completion)).ConfigureAwait(false);await completion.Task.ConfigureAwait(false);slot.Durable=true;
    }
    async Task WriteLoopAsync()
    {
        var batch=new List<Write>(16);
        await foreach(var first in writes.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            batch.Clear();batch.Add(first);await Task.Delay(1).ConfigureAwait(false);
            while(batch.Count<16 && writes.Reader.TryRead(out var next))batch.Add(next);
            try
            {
                if(path is not null)
                {
                    if(lastFailure is not null){Open();RecoverLog(false);}
                    if(log!.Length+batch.Sum(x=>(long)x.Bytes.Length+36)>logLimit || log.Length>Math.Max(16L*1024*1024,RetainedBytes*3))Compact();
                    if(log!.Length+batch.Sum(x=>(long)x.Bytes.Length+36)>logLimit)throw Capacity("Position intent append capacity exhausted.");
                    foreach(var item in batch)Append(log,item.Bytes);log.Flush(true);Interlocked.Increment(ref flushes);
                }
                lock(gate)foreach(var item in batch){committed[item.Key]=item.Bytes;if(slots[item.Key].Kind==PositionIntentKind.ClosedFence)ObserveFence(Decode(item.Bytes));}
                Volatile.Write(ref lastFailure,null);foreach(var item in batch)item.Completion.TrySetResult();
            }
            catch(Exception error)
            {Volatile.Write(ref lastFailure,error.Message);foreach(var item in batch)item.Completion.TrySetException(error);}
        }
    }
    void Open(){if(log is not null)return;log=new(path!,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);}
    static void Append(FileStream target,byte[] bytes)
    {
        Span<byte> header=stackalloc byte[36];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);SHA256.HashData(bytes,header[4..]);target.Write(header);target.Write(bytes);
    }
    void RecoverLog(bool startup)
    {
        var stream=log!;stream.Position=0;var recovered=new Dictionary<Guid,byte[]>();long end=0;Span<byte> header=stackalloc byte[36];
        while(stream.Position<stream.Length)
        {
            if(stream.Length-stream.Position<36)break;
            stream.ReadExactly(header);var size=BinaryPrimitives.ReadInt32LittleEndian(header);
            if(size<1||size>FrameLimit)throw new InvalidDataException("Invalid position intent frame length.");
            if(stream.Length-stream.Position<size)break;var bytes=new byte[size];stream.ReadExactly(bytes);
            if(!CryptographicOperations.FixedTimeEquals(header[4..],SHA256.HashData(bytes)))throw new InvalidDataException("Position intent checksum mismatch.");
            var value=Decode(bytes);Validate(value);recovered[value.Key]=bytes;end=stream.Position;
        }
        if(end!=stream.Length){stream.SetLength(end);stream.Flush(true);}stream.Position=end;
        lock(gate)
        {
            committed.Clear();foreach(var item in recovered)committed.Add(item.Key,item.Value);
            closedFloors.Clear();foreach(var bytes in recovered.Values){var value=Decode(bytes);ObserveFence(value);}
            if(startup)
                foreach(var item in recovered)
                {
                    if(slots.Count>=MaximumKeys)throw new InvalidDataException("Excess position intent keys.");
                    var value=Decode(item.Value);slots.Add(item.Key,new(){Bytes=item.Value,Durable=true,Completed=value.IsCompleted,Kind=value.Kind,Position=value.PositionId,Generation=value.RouteGeneration,Sequence=value.PositionSequence});
                    retained+=item.Value.Length;pendingCount+=value.IsCompleted?0:1;ordinal=Math.Max(ordinal,value.Ordinal);
                }
            if(pendingCount>MaximumPending)throw new InvalidDataException("Excess pending position intents.");
            if(retained>retainedLimit)throw new InvalidDataException("Position replay evidence exceeds configured capacity.");
        }
    }
    void Compact()
    {
        // Only exact latest committed frames are copied. No required replay evidence is pruned here.
        var temporary=path+".compact";byte[][] frames;
        lock(gate)
        {
            // Keep completed evidence until a broker replay floor is available; closed fences suppress old work.
            // Capacity exhaustion backpressures rather than guessing a safe deletion horizon.
            frames=committed.Values.ToArray();
        }
        using(var next=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){foreach(var bytes in frames)Append(next,bytes);next.Flush(true);}
        log!.Dispose();log=null;AtomicDurableFile.Replace(temporary,path!);Open();log!.Position=log.Length;Interlocked.Increment(ref compactions);
    }
    static readonly MessagePackSerializerOptions EncodingOptions=MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.CompositeResolver.Create(PositionIntentResolver.Instance,MessagePack.Resolvers.StandardResolver.Instance));
    static byte[] Encode(PositionRealtimeIntent record)
    {
        var bytes=MessagePackSerializer.Serialize(record,EncodingOptions);if(bytes.Length>FrameLimit)throw new InvalidDataException("Position intent exceeds frame limit.");return bytes;
    }
    static PositionRealtimeIntent Decode(byte[] bytes)=>MessagePackSerializer.Deserialize<PositionRealtimeIntent>(bytes,EncodingOptions);
    static void Validate(PositionRealtimeIntent record)
    {
        if(record.Version!=1 || (record.Flags & ~(PositionIntentFlags.OutputCaptured|PositionIntentFlags.Published|PositionIntentFlags.Cleaned|PositionIntentFlags.Completed|PositionIntentFlags.Retired))!=0 || record.Key==Guid.Empty||record.SourceHash.Length!=64||record.PositionId.PositionId==Guid.Empty||record.RouteGeneration<0||record.PositionSequence<0
            ||!Enum.IsDefined(record.Kind)||record.Request.Length==0||record.Ordinal<=0)throw new InvalidDataException("Invalid position intent.");
    }
    public async ValueTask DisposeAsync()
    {
        lock(gate){if(disposed)return;disposed=true;}writes.Writer.TryComplete();await writer.ConfigureAwait(false);log?.Dispose();lease?.Dispose();
    }
    public void Dispose()=>DisposeAsync().AsTask().GetAwaiter().GetResult();
}
