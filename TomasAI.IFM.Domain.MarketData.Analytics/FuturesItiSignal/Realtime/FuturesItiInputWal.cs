using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Crash-safe local staging before queue admission. Segments are reclaimed only after the shared journal commits.</summary>
public sealed class FuturesItiInputWal : IAsyncDisposable
{
    const long SegmentBytes = 16 * 1024 * 1024;
    const int MaxPayload = 60 * 1024;
    public const long DefaultMaximumRetainedBytes=8L*1024*1024*1024;
    readonly Lock capacityGate=new();
    readonly long maximumRetainedBytes;
    long occupiedBytes;
    /// <summary>Includes durable frames and every admitted but not yet flushed append.</summary>
    public long RetainedAndReservedBytes { get { lock(capacityGate)return occupiedBytes; } }
    public long MaximumRetainedBytes=>maximumRetainedBytes;
    static long ConfiguredMaximumBytes()
    {
        var configured=Environment.GetEnvironmentVariable("IFM_ITI_INPUT_WAL_MAX_BYTES");
        return string.IsNullOrWhiteSpace(configured)?DefaultMaximumRetainedBytes:
            long.TryParse(configured,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var value)&&value>=1024?value:
            throw new InvalidOperationException("IFM_ITI_INPUT_WAL_MAX_BYTES must specify a finite byte limit of at least1024.");
    }
    void Reserve(long bytes)
    {
        lock(capacityGate)
        {
            if(bytes>maximumRetainedBytes-occupiedBytes)throw new IOException("ITI input WAL capacity exhausted; retained inputs were not evicted and must be committed before retry.");
            occupiedBytes+=bytes;
        }
    }
    void Unreserve(long bytes){lock(capacityGate)occupiedBytes-=bytes;}
    readonly SemaphoreSlim gate = new(1, 1);
    readonly string root;
    readonly FuturesItiRecoveryCoverage releases;
    FileStream writer;
    long segment;
    readonly long maximumSegmentBytes;
    sealed class Append(FuturesItiInputWal owner) : IValueTaskSource<Position>
    {
        ManualResetValueTaskSourceCore<Position> completion = new() { RunContinuationsAsynchronously = true };
        int consumed;
        public long ReservedBytes;
        public byte[] Payload = [];
        public byte[][]? BatchPayloads;
        public Position[]? BatchPositions;
        public void Prepare(byte[] payload) { completion.Reset(); consumed = 0; Payload = payload; }
        public ValueTask<Position> Completion => new(this, completion.Version);
        public void Complete(Position value) => completion.SetResult(value);
        public void Fail(Exception error) => completion.SetException(error);
        public Position GetResult(short token)
        {
            if (completion.GetStatus(token) == ValueTaskSourceStatus.Pending)
                throw new InvalidOperationException("A WAL append must complete before its result is consumed.");
            if (Interlocked.Exchange(ref consumed, 1) != 0) throw new InvalidOperationException("A WAL append result can only be consumed once.");
            try { return completion.GetResult(token); }
            finally { owner.Return(this); }
        }
        public ValueTaskSourceStatus GetStatus(short token) => completion.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => completion.OnCompleted(continuation, state, token, flags);
    }
    readonly Lock poolGate = new();
    readonly Stack<Append> pool = new(128);
    Append Rent(byte[] payload)
    {
        Append value;
        lock (poolGate) value = pool.Count == 0 ? new(this) : pool.Pop();
        value.Prepare(payload); return value;
    }
    void Return(Append value)
    {
        value.Payload = [];
        value.ReservedBytes=0;
        value.BatchPayloads=null;value.BatchPositions=null;
        lock (poolGate) if (pool.Count < 128) pool.Push(value);
    }
    readonly Channel<Append> appends = Channel.CreateBounded<Append>(new BoundedChannelOptions(4096)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
    readonly Task commitWorker;
    public Guid RootId { get; }
    public string DirectoryPath => root;

    public FuturesItiInputWal(string directory):this(directory,SegmentBytes,ConfiguredMaximumBytes()){}
    public FuturesItiInputWal(string directory,long segmentBytes):this(directory,segmentBytes,ConfiguredMaximumBytes()){}
    public FuturesItiInputWal(string directory,long segmentBytes,long maximumBytes)
    {
        if(segmentBytes<1024)throw new ArgumentOutOfRangeException(nameof(segmentBytes));
        if(maximumBytes<1024)throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        maximumRetainedBytes=maximumBytes;
        maximumSegmentBytes=segmentBytes;
        root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        foreach(var path in Directory.EnumerateFiles(root,"*.wal"))
        {
            var size=new FileInfo(path).Length;
            if(size>maximumRetainedBytes-occupiedBytes)throw new IOException("Recovered ITI input WAL exceeds its configured capacity; authentic retained history requires maintenance.");
            occupiedBytes+=size;
        }
        var marker = Path.Combine(root, "journal.id");
        if (!File.Exists(marker))
        {
            using var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(Guid.NewGuid().ToByteArray());
            file.Flush(true);
        }
        var id = File.ReadAllBytes(marker);
        if (id.Length != 16) throw new InvalidDataException("Invalid ITI journal-root identity.");
        RootId = new(id);
        releases=new(Path.Combine(root,"source-releases"),RootId);
        segment = Directory.EnumerateFiles(root, "*.wal").Select(path => Parse(path)).DefaultIfEmpty(1).Max();
        writer = Open(segment);
        // Physical disk flushes run on one dedicated thread, never a ThreadPool continuation.
        commitWorker = Task.Factory.StartNew(CommitLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    string PathFor(long value) => Path.Combine(root, value.ToString("D16", System.Globalization.CultureInfo.InvariantCulture) + ".wal");
    static long Parse(string path) => long.Parse(Path.GetFileNameWithoutExtension(path), System.Globalization.CultureInfo.InvariantCulture);
    FileStream Open(long value) => new(PathFor(value), FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan);

    public ValueTask<Position> AppendAsync(byte[] payload, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length is < 1 or > MaxPayload) throw new InvalidDataException("ITI journal input exceeds its frame bound.");
        token.ThrowIfCancellationRequested();
        var reserved=payload.Length+36L;Reserve(reserved);
        Append request;try{request=Rent(payload);request.ReservedBytes=reserved;}catch{Unreserve(reserved);throw;}
        // The caller retains its immutable payload until this operation completes.
        if (appends.Writer.TryWrite(request)) return request.Completion;
        return EnqueueAsync(request, token);
    }
    /// <summary>Every original input is frozen before one queued group obtains its durable flush.</summary>
    public async ValueTask<Position[]> AppendBatchAsync(IReadOnlyList<byte[]> payloads,CancellationToken token)
    {
        if(payloads.Count is <1 or >64)throw new ArgumentOutOfRangeException(nameof(payloads),"An ITI durable group contains 1–64 inputs.");
        foreach(var payload in payloads)if(payload is null||payload.Length is <1 or >MaxPayload)throw new InvalidDataException("ITI journal batch exceeds its frame bound.");
        token.ThrowIfCancellationRequested();var reserved=payloads.Sum(payload=>payload.Length+36L);Reserve(reserved);
        Append request;Position[] positions;
        try{positions=new Position[payloads.Count];request=Rent([]);request.ReservedBytes=reserved;request.BatchPayloads=payloads.ToArray();request.BatchPositions=positions;}
        catch{Unreserve(reserved);throw;}
        if(appends.Writer.TryWrite(request))await request.Completion.ConfigureAwait(false);
        else await EnqueueAsync(request,token).ConfigureAwait(false);
        return positions;
    }
    async ValueTask<Position> EnqueueAsync(Append request, CancellationToken token)
    {
        try { await appends.Writer.WriteAsync(request, token).ConfigureAwait(false); }
        catch { Unreserve(request.ReservedBytes);Return(request); throw; }
        // After enqueue, join durable completion even on cancellation. No early success or abandoned buffer.
        return await request.Completion.ConfigureAwait(false);
    }

    void CommitLoop()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(MaxPayload + 36);
        var group = new List<(Append Request, Position Position)>(64);
        Exception? failure = null;
        try
        {
            while (appends.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                gate.Wait();
                try
                {
                    while (group.Count < 64 && appends.Reader.TryRead(out var request))
                    {
                        // Add before touching disk so a failed write also completes this caller exceptionally.
                        group.Add((request, default));
                        var payloadCount=request.BatchPayloads?.Length??1;
                        for(var index=0;index<payloadCount;index++)
                        {
                            if (writer.Length >= maximumSegmentBytes)
                            {
                                writer.Flush(true); writer.Dispose(); writer = Open(++segment);
                            }
                            var payload=request.BatchPayloads is{} payloads?payloads[index]:request.Payload;
                            BinaryPrimitives.WriteInt32LittleEndian(buffer,payload.Length);
                            SHA256.HashData(payload,buffer.AsSpan(4,32));payload.CopyTo(buffer,36);
                            writer.Write(buffer,0,payload.Length+36);
                            var position=new Position(segment,writer.Position);
                            if(request.BatchPositions is{} positions)positions[index]=position;
                            group[^1]=(request,position);
                        }
                    }
                    writer.Flush(true);
                    foreach (var item in group) item.Request.Complete(item.Position);
                    group.Clear();
                }
                finally { gate.Release(); }
            }
        }
        catch (Exception error)
        {
            failure = error; appends.Writer.TryComplete(error);
            foreach (var item in group) item.Request.Fail(error);
            while (appends.Reader.TryRead(out var request)) request.Fail(error);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (failure is not null) writer.Dispose();
        }
    }

    /// <summary>A prepared input may execute only after the exact original source has confirmed recipient capture.</summary>
    public bool IsSourceReleased(TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands.GenerateFuturesItiSignalCommand input)
        =>!input.SourceCaptureRequired||releases.Contains(input.CommandId,input.JournalPayload??TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiJournalEnvelope.Encode(input));
    public void ReleaseSource(TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands.GenerateFuturesItiSignalCommand input)
    {
        if(!input.SourceCaptureRequired)return;
        if(input.SourceEventId==Guid.Empty)throw new InvalidDataException("Prepared ITI source has no immutable source identity.");
        releases.Record(input.CommandId,input.JournalPayload??TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiJournalEnvelope.Encode(input));
    }
    public void ReleaseSourcesBatch(IReadOnlyList<TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands.GenerateFuturesItiSignalCommand> inputs)
    {
        var rows=inputs.Where(input=>input.SourceCaptureRequired).Select(input=>
        {
            if(input.SourceEventId==Guid.Empty)throw new InvalidDataException("Prepared ITI batch has no immutable source identity.");
            return(input.CommandId,input.JournalPayload??TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiJournalEnvelope.Encode(input));
        }).ToArray();
        releases.RecordBatch(rows);
    }

    public async IAsyncEnumerable<Record> ReplayAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.wal").OrderBy(Parse))
        {
            await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var header = new byte[36];
            while (reader.Position < reader.Length)
            {
                await reader.ReadExactlyAsync(header, token).ConfigureAwait(false);
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length is < 1 or > MaxPayload) throw new InvalidDataException("Corrupt ITI WAL length; authentic source recovery is required.");
                var payload = new byte[length];
                await reader.ReadExactlyAsync(payload, token).ConfigureAwait(false);
                if (!SHA256.HashData(payload).AsSpan().SequenceEqual(header.AsSpan(4,32)))
                    throw new InvalidDataException("Corrupt ITI WAL checksum; authentic source recovery is required.");
                yield return new(new(Parse(path), reader.Position), payload);
            }
        }
    }

    /// <summary>Only a confirmed shared-journal commit can acknowledge local staging.</summary>
    public async ValueTask AcknowledgeAsync(Position position)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var retired=false;
            // A completely committed active segment may be sealed even at the
            // byte ceiling, so capacity refusal cannot prevent its reclamation.
            if(position.Segment==segment&&position.EndOffset==writer.Length)
            {
                writer.Flush(true);writer.Dispose();writer=Open(++segment);
            }
            foreach (var path in Directory.EnumerateFiles(root, "*.wal"))
            {
                var value = Parse(path);
                if (value < segment && (value < position.Segment || value == position.Segment && position.EndOffset == new FileInfo(path).Length))
                {
                    var target = Path.GetFullPath(path);
                    if (!string.Equals(Path.GetDirectoryName(target), root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("ITI WAL target escaped its journal directory.");
                    var reclaimed=new FileInfo(target).Length;
                    File.Delete(target);
                    Unreserve(reclaimed);
                    retired=true;
                }
            }
            if(retired&&releases.Count>0)
            {
                var active=new HashSet<Guid>();
                await foreach(var record in ReplayAsync().ConfigureAwait(false))
                {
                    var input=TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiJournalEnvelope.Decode(record.Payload,0);
                    if(input.SourceCaptureRequired)active.Add(input.CommandId);
                }
                releases.Retain(active);
            }
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        appends.Writer.TryComplete();
        await commitWorker.ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try { await writer.DisposeAsync().ConfigureAwait(false); releases.Dispose(); }
        finally { gate.Release(); gate.Dispose(); }
    }

    public readonly record struct Position(long Segment, long EndOffset);
    public sealed record Record(Position Position, byte[] Payload);
}
