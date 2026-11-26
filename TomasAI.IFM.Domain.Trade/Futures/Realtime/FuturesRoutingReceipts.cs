using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime;

public enum FuturesRouteStage : byte { Pending, Confirmed, Superseded }
public sealed record FuturesRoutingReceipt(FuturesTickTradeDataChangedEvent Source, string Fingerprint, long Ordinal,
    ChangeTradeLegDataCommand[] Commands, FuturesRouteStage[] Stages, bool Completed = false);

/// <summary>Single-owner binary fan-out receipts. Command confirmation precedes durable stream-cursor advancement.</summary>
public sealed class FuturesRoutingReceipts : IDisposable
{
    const int MaximumBytes = 8 * 1024 * 1024, PendingLimit = 4096, CacheLimit = 128, MaximumBranches = 4096;
    readonly Lock gate = new();
    readonly string? root;
    readonly FileStream? lease;
    readonly DurableJournalCapacity? capacity;
    public long MaximumRetainedBytes=>capacity?.MaximumBytes??0;
    public long RetainedBytes=>capacity?.RetainedBytes??0;
    public long SettlementReservedBytes=>capacity?.SettlementReservedBytes??0;
    readonly Dictionary<Guid, string> pending = [];
    readonly Dictionary<Guid, byte[]> cache = [];
    readonly LinkedList<Guid> lru = new();
    readonly Dictionary<Guid, LinkedListNode<Guid>> nodes = [];
    readonly Dictionary<string, long> cursors = new(StringComparer.Ordinal);
    long ordinal;
    sealed class Lane { public readonly SemaphoreSlim Gate = new(1,1); public int References; }
    readonly Dictionary<string,Lane> lanes = new(StringComparer.Ordinal);
    public async ValueTask SerializeAsync(string contract, Func<Task> action)
    {
        Lane lane;
        lock (gate)
        {
            if (!lanes.TryGetValue(contract,out lane!))
            {
                if (lanes.Count >= PendingLimit) throw new IOException("Futures routing active-contract capacity exhausted; retain source.");
                lanes.Add(contract,lane = new());
            }
            lane.References++;
        }
        try
        {
            await lane.Gate.WaitAsync().ConfigureAwait(false);
            try { await action().ConfigureAwait(false); }
            finally { lane.Gate.Release(); }
        }
        finally
        {
            lock (gate) if (--lane.References == 0) { lanes.Remove(contract); lane.Gate.Dispose(); }
        }
    }

    /// <summary>Null directory is explicitly volatile for isolated tests only.</summary>
    public FuturesRoutingReceipts(string? directory = null):this(directory,DurableJournalCapacity.ConfiguredMaximum("IFM_FUTURES_ROUTING_MAX_BYTES")){}
    public FuturesRoutingReceipts(string? directory,long maximumRetainedBytes)
    {
        if (directory is null) return;
        root = Path.GetFullPath(directory); AtomicDurableFile.EnsureDirectory(root);
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            foreach (var name in new[] { "pending", "history", "cursors" }) AtomicDurableFile.EnsureDirectory(Path.Combine(root,name));
            for (var shard = 0; shard < 256; shard++) AtomicDurableFile.EnsureDirectory(Path.Combine(root,"history",shard.ToString("x2",System.Globalization.CultureInfo.InvariantCulture)));
            capacity=new(root,maximumRetainedBytes,MaximumBytes);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root,"pending"),"*.frt"))
            {
                var id = Guid.ParseExact(Path.GetFileNameWithoutExtension(file),"N");
                var receipt = Decode(Read(file)); Validate(id,receipt);
                capacity.ReserveSettlement(file,Path.Combine(root,"cursors",Stream(receipt.Source)+".cur"),40);
                if (pending.Count >= PendingLimit) throw new IOException("Futures routing pending capacity exhausted.");
                pending.Add(id,receipt.Source.TickDataId.ContractId); ordinal = Math.Max(ordinal,receipt.Ordinal);
            }
        }
        catch { lease.Dispose(); throw; }
    }
    string PendingPath(Guid id) => Path.Combine(root!,"pending",id.ToString("N")+".frt");
    string HistoryPath(Guid id) => Path.Combine(root!,"history",id.ToString("N")[..2],id.ToString("N")+".frt");
    public static string Stream(FuturesTickTradeDataChangedEvent source) => Convert.ToHexString(SHA256.HashData(
        MessagePackSerializer.Serialize(new object[] { source.TickDataId.ContractId, source.TickDataId.ValueDate, source.SourceDataset, source.SourceGenerationId })));
    public static string Fingerprint(FuturesTickTradeDataChangedEvent source) => Convert.ToHexString(SHA256.HashData(
        MessagePackSerializer.Serialize(new object[] { source, source.SourceDataset, source.SourceGenerationId })));
    public FuturesRoutingReceipt? Find(FuturesTickTradeDataChangedEvent source)
    {
        lock (gate)
        {
            var result = FindId(source.Id);
            if (result is not null && result.Fingerprint != Fingerprint(source)) throw new InvalidDataException("Futures routing source ID reused with different content.");
            return result;
        }
    }
    FuturesRoutingReceipt? FindId(Guid id)
    {
        var completed = !pending.ContainsKey(id);
        var bytes = cache.GetValueOrDefault(id);
        if (bytes is null && root is not null)
        {
            try { bytes = Read(PendingPath(id)); completed = false; }
            catch (FileNotFoundException)
            {
                try { bytes = Read(HistoryPath(id)); completed = true; }
                catch (FileNotFoundException) { return null; }
            }
        }
        if (bytes is null) return null;
        var result = Decode(bytes) with { Completed = completed }; Validate(id,result); Remember(id,bytes); return result;
    }
    public bool IsCompletedSequence(FuturesTickTradeDataChangedEvent source)
    { lock (gate) return source.TradeData.SourceSequence <= Cursor(Stream(source)); }
    long Cursor(string stream)
    {
        if (cursors.TryGetValue(stream,out var cursor)) return cursor;
        cursor = 0;
        if (root is not null)
            try
            {
                var bytes = Read(Path.Combine(root,"cursors",stream+".cur")); VerifyFrame(bytes);
                if (bytes.Length != 40) throw new InvalidDataException("Invalid Futures routing cursor.");
                cursor = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(32));
                if (cursor <= 0) throw new InvalidDataException("Invalid Futures routing sequence.");
            }
            catch (FileNotFoundException) { }
        if (cursors.Count >= 4096) cursors.Clear(); // Bounded cache; durable cursor remains authoritative.
        cursors.Add(stream,cursor); return cursor;
    }
    public FuturesRoutingReceipt Prepare(FuturesTickTradeDataChangedEvent source, ChangeTradeLegDataCommand[] commands)
    {
        lock (gate)
        {
            var existing = Find(source); if (existing is not null) return existing;
            if (pending.Count >= PendingLimit) throw new IOException("Futures routing pending capacity exhausted; retain source.");
            var receipt = new FuturesRoutingReceipt(source,Fingerprint(source),checked(++ordinal),commands,new FuturesRouteStage[commands.Length]);
            Validate(source.Id,receipt); pending.Add(source.Id,source.TickDataId.ContractId);
            try { Write(receipt); }
            catch
            {
                if (root is null) pending.Remove(source.Id);
                else
                    try { _ = File.GetAttributes(PendingPath(source.Id)); }
                    catch (FileNotFoundException) { pending.Remove(source.Id); }
                throw;
            }
            return FindId(source.Id)!;
        }
    }
    public void SetStage(Guid id, int branch, FuturesRouteStage stage)
    {
        lock (gate)
        {
            var receipt = FindId(id) ?? throw new InvalidDataException("Missing Futures routing capture.");
            if (stage is not (FuturesRouteStage.Confirmed or FuturesRouteStage.Superseded) || branch < 0 || branch >= receipt.Stages.Length)
                throw new ArgumentOutOfRangeException(nameof(branch));
            if (receipt.Stages[branch] != FuturesRouteStage.Pending) return;
            receipt.Stages[branch] = stage; Write(receipt);
        }
    }
    public void Complete(Guid id)
    {
        lock (gate)
        {
            var receipt = FindId(id) ?? throw new InvalidDataException("Missing Futures routing capture.");
            if (receipt.Stages.Any(stage => stage == FuturesRouteStage.Pending)) throw new InvalidOperationException("Cannot advance routing cursor with unfinished branches.");
            var stream = Stream(receipt.Source); var sequence = receipt.Source.TradeData.SourceSequence;
            if (sequence > Cursor(stream))
            {
                if (root is not null)
                {
                    var bytes = new byte[40]; System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(32),sequence);
                    SHA256.HashData(bytes.AsSpan(32),bytes.AsSpan(0,32));
                    WriteFrame(Path.Combine(root,"cursors",stream+".cur"),bytes,settlementOwner:PendingPath(id));
                }
                cursors[stream] = sequence;
            }
            if (root is not null && !receipt.Completed)
            {
                using var mutation = RealtimeActorGeneration.EnterMutation(); Forget(id);
                try { capacity!.Move(PendingPath(id),HistoryPath(id),()=>AtomicDurableFile.Replace(PendingPath(id),HistoryPath(id))); }
                catch
                {
                    // Re-read either location after an ambiguous durable rename.
                    Forget(id); throw;
                }
            }
            pending.Remove(id);
        }
    }
    public FuturesRoutingReceipt[] Pending(string? contract = null)
    {
        lock (gate) return pending.Where(pair => contract is null || string.Equals(pair.Value,contract,StringComparison.Ordinal))
            .Select(pair => FindId(pair.Key) ?? throw new InvalidDataException("Missing pending routing capture.")).OrderBy(receipt => receipt.Ordinal).ToArray();
    }
    void Write(FuturesRoutingReceipt receipt)
    {
        Validate(receipt.Source.Id,receipt);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer,System.Text.Encoding.UTF8,true))
        {
            writer.Write(1); writer.Write(receipt.Fingerprint); writer.Write(receipt.Ordinal);
            var source = MessagePackSerializer.Serialize(receipt.Source); writer.Write(source.Length); writer.Write(source);
            writer.Write(receipt.Source.SourceDataset); writer.Write(receipt.Source.SourceGenerationId.ToByteArray());
            WriteOrigin(writer,receipt.Source);
            writer.Write(receipt.Commands.Length);
            for (var i=0;i<receipt.Commands.Length;i++)
            {
                writer.Write((byte)receipt.Stages[i]); var command = MessagePackSerializer.Serialize(receipt.Commands[i]); writer.Write(command.Length); writer.Write(command);
                WriteOrigin(writer,receipt.Commands[i]);
            }
        }
        if (buffer.Length > MaximumBytes-32) throw new InvalidDataException("Routing capture exceeds frame limit.");
        var bytes = new byte[32+buffer.Length]; buffer.GetBuffer().AsSpan(0,(int)buffer.Length).CopyTo(bytes.AsSpan(32));
        SHA256.HashData(bytes.AsSpan(32),bytes.AsSpan(0,32));
        Forget(receipt.Source.Id);
        if (root is not null) WriteFrame(PendingPath(receipt.Source.Id),bytes,Path.Combine(root,"cursors",Stream(receipt.Source)+".cur"),40);
        Remember(receipt.Source.Id,bytes);
    }
    static void WriteOrigin(BinaryWriter writer,object value)
    { var origin = MessageOrigins.Get(value); writer.Write(origin is not null); if (origin is not null) writer.Write(MessageOriginBinary.Serialize(origin)); }
    static T RestoreOrigin<T>(BinaryReader reader,T value) where T:class => MessageOrigins.Restore(value,reader.ReadBoolean()?MessageOriginBinary.Deserialize(reader.ReadString()):null);
    void WriteFrame(string path,byte[] bytes,string? settlementPath=null,int settlementLength=0,string? settlementOwner=null)
    {
        capacity!.Write(path,bytes.Length,()=>
        {
            using var mutation = RealtimeActorGeneration.EnterMutation();
            using (var file = new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)) { file.Write(bytes); file.Flush(true); }
            AtomicDurableFile.Replace(path+".tmp",path);
        },settlementPath,settlementLength,settlementOwner);
    }
    static byte[] Read(string path)
    {
        using var file = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if (file.Length is <33 or >MaximumBytes) throw new InvalidDataException("Invalid routing frame size.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); return bytes;
    }
    static void VerifyFrame(byte[] bytes)
    { if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(32)),bytes.AsSpan(0,32))) throw new InvalidDataException("Routing recovery checksum mismatch."); }
    static byte[] ReadPayload(BinaryReader reader)
    {
        var size=reader.ReadInt32(); if (size is <1 or >MaximumBytes || size>reader.BaseStream.Length-reader.BaseStream.Position) throw new InvalidDataException("Invalid routing payload length.");
        return reader.ReadBytes(size);
    }
    static FuturesRoutingReceipt Decode(byte[] bytes)
    {
        VerifyFrame(bytes); using var reader = new BinaryReader(new MemoryStream(bytes,32,bytes.Length-32,false),System.Text.Encoding.UTF8);
        if (reader.ReadInt32()!=1) throw new InvalidDataException("Unsupported routing receipt version.");
        var hash=reader.ReadString(); var order=reader.ReadInt64(); var source=MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(ReadPayload(reader));
        source=source with { SourceDataset=reader.ReadString(), SourceGenerationId=new Guid(reader.ReadBytes(16)) }; source=RestoreOrigin(reader,source);
        var count=reader.ReadInt32(); if (count is <0 or >MaximumBranches) throw new InvalidDataException("Invalid routing fan-out count.");
        var commands=new ChangeTradeLegDataCommand[count]; var stages=new FuturesRouteStage[count];
        for(var i=0;i<count;i++) { stages[i]=(FuturesRouteStage)reader.ReadByte(); commands[i]=RestoreOrigin(reader,MessagePackSerializer.Deserialize<ChangeTradeLegDataCommand>(ReadPayload(reader))); }
        if(reader.BaseStream.Position!=reader.BaseStream.Length) throw new InvalidDataException("Trailing routing receipt bytes.");
        return new(source,hash,order,commands,stages);
    }
    static void Validate(Guid id,FuturesRoutingReceipt receipt)
    {
        if(id==Guid.Empty || receipt.Source.Id!=id || receipt.Ordinal<=0 || receipt.Fingerprint!=Fingerprint(receipt.Source)
            || string.IsNullOrWhiteSpace(receipt.Source.TickDataId.ContractId) || receipt.Source.TradeData.SourceSequence<=0
            || receipt.Commands.Length>MaximumBranches || receipt.Commands.Length!=receipt.Stages.Length || receipt.Stages.Any(stage=>!Enum.IsDefined(stage)))
            throw new InvalidDataException("Invalid routing source capture.");
        if(receipt.Commands.Select(command=>command.CommandId).Distinct().Count()!=receipt.Commands.Length)
            throw new InvalidDataException("Routing command identity collision.");
        foreach(var command in receipt.Commands)
            if(command.CommandId==Guid.Empty || command.ContractId!=receipt.Source.TickDataId.ContractId || command.SourceSequence!=receipt.Source.TradeData.SourceSequence
                || command.RouteGeneration<=0 || command.EffectiveAtUtc.Kind!=DateTimeKind.Utc)
                throw new InvalidDataException("Invalid captured routing command.");
    }
    void Forget(Guid id) { cache.Remove(id); if(nodes.Remove(id,out var node)) lru.Remove(node); }
    void Remember(Guid id,byte[] bytes)
    { Forget(id); cache.Add(id,bytes); nodes.Add(id,lru.AddLast(id)); if(root is not null) while(cache.Count>CacheLimit) Forget(lru.First!.Value); }
    public void Dispose()=>lease?.Dispose();
}
