using System.Diagnostics;
using System.Security.Cryptography;
using MessagePack;
namespace TomasAI.IFM.Application.MarketData.Rust.Persistence;

/// <summary>Copied raw-reference page request. No field carries a native resource.</summary>
[MessagePackObject]
public sealed record NativeReferenceRequest([property:Key(0)] ulong Id,[property:Key(1)] byte Kind,[property:Key(2)] byte[] Arguments,[property:Key(3)] uint Offset);
/// <summary>One immutable page and its full-response integrity identity.</summary>
[MessagePackObject]
public sealed record NativeReferencePage([property:Key(0)] ulong Id,[property:Key(1)] uint Total,[property:Key(2)] uint Offset,
    [property:Key(3)] string Digest,[property:Key(4)] byte[] Bytes,[property:Key(5)] string? Error);
/// <summary>External data-only store port. Implementations read/write copied
/// reference facts only; they must not acquire native subscriptions or contexts.</summary>
public interface INativeReferenceFacts
{
    /// <summary>Reads one bounded raw fact snapshot for the native requester.</summary>
    Task<byte[]> ReadAsync(byte kind,ReadOnlyMemory<byte> arguments,CancellationToken cancellationToken);
}
/// <summary>Bounded immutable paging outside native feed ownership. The cache is
/// at most eight snapshots / 32 MiB; expiry is monotonic IO retention, unrelated
/// to business ValueDate. A stalled database job never holds a Rust lease.</summary>
public sealed class NativeReferenceStorage(INativeReferenceFacts facts)
{
    const int Maximum=4*1024*1024,Page=4096;
    readonly object gate=new();
    readonly Dictionary<ulong,Entry> entries=[];
    sealed class Entry(string fingerprint)
    {
        internal readonly string Fingerprint=fingerprint;
        internal readonly long Created=Stopwatch.GetTimestamp();
        internal bool Delivered;
        internal readonly TaskCompletionSource<Blob> Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    sealed record Blob(byte[] Bytes,string Digest);
    /// <summary>Reads exact immutable pages; identifier/argument conflicts cannot
    /// swap a native request onto a different raw database snapshot.</summary>
    public async Task<NativeReferencePage> ReadAsync(NativeReferenceRequest request,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.Id==0 || request.Kind is <1 or >10 || request.Arguments is null || request.Arguments.Length>16*1024 || request.Offset>=Maximum || request.Offset%Page!=0)
            throw new InvalidDataException("Invalid native raw-reference request.");
        var fingerprint=$"{request.Kind}:{Convert.ToHexString(SHA256.HashData(request.Arguments))}";
        Entry entry;bool creator=false;
        lock(gate)
        {
            foreach(var stale in entries.Where(x=>x.Value.Completion.Task.IsCompleted && Stopwatch.GetElapsedTime(x.Value.Created)>=TimeSpan.FromMinutes(2)).Select(x=>x.Key).ToArray())entries.Remove(stale);
            if(entries.TryGetValue(request.Id,out var found))
            {entry=found;if(entry.Fingerprint!=fingerprint)throw new InvalidDataException("Raw-reference request identity conflict.");}
            else
            {
                if(request.Offset!=0)throw new InvalidDataException("Raw-reference snapshot expired.");
                if(entries.Count>=8){
                    // Reclaim completed transfers, never an in-progress paged
                    // read or stalled database operation. Sequential native
                    // source batches must not wait two minutes for capacity.
                    var completed=entries.Where(item=>item.Value.Delivered).OrderBy(item=>item.Value.Created).FirstOrDefault();
                    if(completed.Value is not null)entries.Remove(completed.Key);
                }
                if(entries.Count>=8)throw new InvalidOperationException("Raw-reference snapshot capacity exhausted.");
                entries.Add(request.Id,entry=new(fingerprint));creator=true;
            }
        }
        if(creator)
        {
            try
            {
                var bytes=await facts.ReadAsync(request.Kind,request.Arguments,token).ConfigureAwait(false);
                if(bytes is null || bytes.Length>Maximum)throw new InvalidDataException("Raw-reference snapshot capacity exceeded.");
                // Snapshot storage owns its copy even if a database implementation
                // reuses or mutates its returned buffer after the call completes.
                bytes=bytes.ToArray();entry.Completion.TrySetResult(new(bytes,Convert.ToHexString(SHA256.HashData(bytes))));
            }
            catch(Exception error)
            {
                entry.Completion.TrySetException(error);_ = entry.Completion.Task.Exception;
                lock(gate){if(entries.GetValueOrDefault(request.Id)==entry)entries.Remove(request.Id);}
                throw;
            }
        }
        var blob=await entry.Completion.Task.WaitAsync(token).ConfigureAwait(false);
        if(request.Offset>blob.Bytes.Length)throw new InvalidDataException("Raw-reference page offset exceeds snapshot.");
        var count=Math.Min(Page,blob.Bytes.Length-(int)request.Offset);
        var page=blob.Bytes.AsSpan((int)request.Offset,count).ToArray();
        if((long)request.Offset+count==blob.Bytes.Length)lock(gate)entry.Delivered=true;
        return new(request.Id,(uint)blob.Bytes.Length,request.Offset,blob.Digest,page,null);
    }
}
