using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

public enum OptionRouteStage : byte { Pending, Confirmed, Superseded }
public sealed record FuturesOptionRoutingReceipt(FuturesTickTradeDataChangedEvent Source, string Fingerprint, long Ordinal,
    ChangeTradeLegDataCommand[] Commands, OptionRouteStage[] Stages,
    bool PublicationsCaptured, TradeRealtimeSnapshotChangedEvent[] Publications, bool[] Published, bool Completed = false);

/// <summary>Single-owner binary option fan-out and publication receipts; arrival ordering does not depend on resettable provider sequences.</summary>
public sealed class FuturesOptionRoutingReceipts : IDisposable
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
                if (lanes.Count >= PendingLimit) throw new IOException("FuturesOption routing active-contract capacity exhausted; retain source.");
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
    public FuturesOptionRoutingReceipts(string? directory = null):this(directory,DurableJournalCapacity.ConfiguredMaximum("IFM_FUTURES_OPTION_ROUTING_MAX_BYTES")){}
    public FuturesOptionRoutingReceipts(string? directory,long maximumRetainedBytes)
    {
        if (directory is null) return;
        root = Path.GetFullPath(directory); AtomicDurableFile.EnsureDirectory(root);
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            foreach (var name in new[] { "pending", "history" }) AtomicDurableFile.EnsureDirectory(Path.Combine(root,name));
            for (var shard = 0; shard < 256; shard++) AtomicDurableFile.EnsureDirectory(Path.Combine(root,"history",shard.ToString("x2",System.Globalization.CultureInfo.InvariantCulture)));
            capacity=new(root,maximumRetainedBytes,MaximumBytes);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root,"pending"),"*.ort"))
            {
                var id = Guid.ParseExact(Path.GetFileNameWithoutExtension(file),"N");
                var receipt = Decode(Read(file)); Validate(id,receipt);
                if (pending.Count >= PendingLimit) throw new IOException("FuturesOption routing pending capacity exhausted.");
                pending.Add(id,Contract(receipt.Source)); ordinal = Math.Max(ordinal,receipt.Ordinal);
            }
        }
        catch { lease.Dispose(); throw; }
    }
    string PendingPath(Guid id) => Path.Combine(root!,"pending",id.ToString("N")+".ort");
    string HistoryPath(Guid id) => Path.Combine(root!,"history",id.ToString("N")[..2],id.ToString("N")+".ort");
    public static string Contract(FuturesTickTradeDataChangedEvent source) => source.OptionMarketPriceObservation?.OptionTickData.ContractId ?? source.TickDataId.ContractId;
    public static long Sequence(FuturesTickTradeDataChangedEvent source) => source.OptionMarketPriceObservation?.SourceSequence ?? source.TradeData.SourceSequence;
    public static Guid Identity(FuturesTickTradeDataChangedEvent source) => source.OptionMarketPriceObservation?.OptionTickData is { } observation
        ? TradePlanContractIdentity.DeterministicId($"{source.Id:N}|{observation.SourceGeneration:N}|{observation.ObservationRevision}|{observation.IsPricingUpdate}") : source.Id;
    public static string Fingerprint(FuturesTickTradeDataChangedEvent source) => Convert.ToHexString(SHA256.HashData(
        MessagePackSerializer.Serialize(new object[] { source, source.SourceDataset, source.SourceGenerationId })));
    public FuturesOptionRoutingReceipt? Find(FuturesTickTradeDataChangedEvent source)
    {
        lock (gate)
        {
            var result = FindId(Identity(source));
            if (result is not null && result.Fingerprint != Fingerprint(source)) throw new InvalidDataException("FuturesOption routing source ID reused with different content.");
            return result;
        }
    }
    FuturesOptionRoutingReceipt? FindId(Guid id)
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
    public FuturesOptionRoutingReceipt Prepare(FuturesTickTradeDataChangedEvent source, ChangeTradeLegDataCommand[] commands)
    {
        lock (gate)
        {
            var existing = Find(source); if (existing is not null) return existing;
            if (pending.Count >= PendingLimit) throw new IOException("FuturesOption routing pending capacity exhausted; retain source.");
            var receipt = new FuturesOptionRoutingReceipt(source,Fingerprint(source),checked(++ordinal),commands,new OptionRouteStage[commands.Length], false, [], []);
            Validate(Identity(source),receipt); pending.Add(Identity(source),Contract(source));
            try { Write(receipt); }
            catch
            {
                if (root is null) pending.Remove(Identity(source));
                else
                    try { _ = File.GetAttributes(PendingPath(Identity(source))); }
                    catch (FileNotFoundException) { pending.Remove(Identity(source)); }
                throw;
            }
            return FindId(Identity(source))!;
        }
    }
    public void SetStage(Guid id, int branch, OptionRouteStage stage)
    {
        lock (gate)
        {
            var receipt = FindId(id) ?? throw new InvalidDataException("Missing FuturesOption routing capture.");
            if (stage is not (OptionRouteStage.Confirmed or OptionRouteStage.Superseded) || branch < 0 || branch >= receipt.Stages.Length)
                throw new ArgumentOutOfRangeException(nameof(branch));
            if (receipt.Stages[branch] != OptionRouteStage.Pending) return;
            receipt.Stages[branch] = stage; Write(receipt);
        }
    }
    public void CapturePublications(Guid id, TradeRealtimeSnapshotChangedEvent[] publications)
    {
        lock (gate)
        {
            var receipt = FindId(id) ?? throw new InvalidDataException("Missing option routing capture.");
            if (receipt.PublicationsCaptured) return;
            Write(receipt with { PublicationsCaptured = true, Publications = publications, Published = new bool[publications.Length] });
        }
    }
    public void ConfirmPublication(Guid id, int index)
    {
        lock (gate)
        {
            var receipt = FindId(id) ?? throw new InvalidDataException("Missing option routing capture.");
            if (!receipt.PublicationsCaptured) throw new InvalidOperationException("Publication was not captured.");
            if (receipt.Published[index]) return;
            receipt.Published[index] = true; Write(receipt);
        }
    }
    public void Complete(Guid id)
    {
        lock (gate)
        {
            var receipt = FindId(id) ?? throw new InvalidDataException("Missing FuturesOption routing capture.");
            if (receipt.Stages.Any(stage => stage == OptionRouteStage.Pending)) throw new InvalidOperationException("Cannot complete option routing with unfinished branches.");
            if (!receipt.PublicationsCaptured || receipt.Published.Any(done => !done)) throw new InvalidOperationException("Option snapshot publication is unfinished.");
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
    public FuturesOptionRoutingReceipt[] Pending(string? contract = null)
    {
        lock (gate) return pending.Where(pair => contract is null || string.Equals(pair.Value,contract,StringComparison.Ordinal))
            .Select(pair => FindId(pair.Key) ?? throw new InvalidDataException("Missing pending routing capture.")).OrderBy(receipt => receipt.Ordinal).ToArray();
    }
    void Write(FuturesOptionRoutingReceipt receipt)
    {
        Validate(Identity(receipt.Source),receipt);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer,System.Text.Encoding.UTF8,true))
        {
            writer.Write(1); writer.Write(receipt.Fingerprint); writer.Write(receipt.Ordinal);
            var source = MessagePackSerializer.Serialize(receipt.Source); writer.Write(source.Length); writer.Write(source);
            writer.Write(receipt.Source.SourceDataset); writer.Write(receipt.Source.SourceGenerationId.ToByteArray());
            WriteOrigin(writer,receipt.Source);
            writer.Write(receipt.PublicationsCaptured); writer.Write(receipt.Publications.Length);
            for (var i=0;i<receipt.Publications.Length;i++)
            {
                writer.Write(receipt.Published[i]); var publication=MessagePackSerializer.Serialize(receipt.Publications[i]);
                writer.Write(publication.Length); writer.Write(publication); WriteOrigin(writer,receipt.Publications[i]);
            }
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
        Forget(Identity(receipt.Source));
        if (root is not null) WriteFrame(PendingPath(Identity(receipt.Source)),bytes);
        Remember(Identity(receipt.Source),bytes);
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
    static FuturesOptionRoutingReceipt Decode(byte[] bytes)
    {
        VerifyFrame(bytes); using var reader = new BinaryReader(new MemoryStream(bytes,32,bytes.Length-32,false),System.Text.Encoding.UTF8);
        if (reader.ReadInt32()!=1) throw new InvalidDataException("Unsupported routing receipt version.");
        var hash=reader.ReadString(); var order=reader.ReadInt64(); var source=MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(ReadPayload(reader));
        source=source with { SourceDataset=reader.ReadString(), SourceGenerationId=new Guid(reader.ReadBytes(16)) }; source=RestoreOrigin(reader,source);
        var captured=reader.ReadBoolean();var publicationCount=reader.ReadInt32();
        if(publicationCount is <0 or >MaximumBranches)throw new InvalidDataException("Invalid option publication count.");
        var publications=new TradeRealtimeSnapshotChangedEvent[publicationCount];var published=new bool[publicationCount];
        for(var i=0;i<publicationCount;i++){published[i]=reader.ReadBoolean();publications[i]=RestoreOrigin(reader,MessagePackSerializer.Deserialize<TradeRealtimeSnapshotChangedEvent>(ReadPayload(reader)));}
        var count=reader.ReadInt32(); if (count is <0 or >MaximumBranches) throw new InvalidDataException("Invalid routing fan-out count.");
        var commands=new ChangeTradeLegDataCommand[count]; var stages=new OptionRouteStage[count];
        for(var i=0;i<count;i++) { stages[i]=(OptionRouteStage)reader.ReadByte(); commands[i]=RestoreOrigin(reader,MessagePackSerializer.Deserialize<ChangeTradeLegDataCommand>(ReadPayload(reader))); }
        if(reader.BaseStream.Position!=reader.BaseStream.Length) throw new InvalidDataException("Trailing routing receipt bytes.");
        return new(source,hash,order,commands,stages,captured,publications,published);
    }
    static void Validate(Guid id,FuturesOptionRoutingReceipt receipt)
    {
        if(id==Guid.Empty || Identity(receipt.Source)!=id || receipt.Ordinal<=0 || receipt.Fingerprint!=Fingerprint(receipt.Source)
            || string.IsNullOrWhiteSpace(Contract(receipt.Source)) || Sequence(receipt.Source)<0
            || receipt.Commands.Length>MaximumBranches || receipt.Commands.Length!=receipt.Stages.Length || receipt.Stages.Any(stage=>!Enum.IsDefined(stage)))
            throw new InvalidDataException("Invalid routing source capture.");
        if(receipt.Publications.Length>MaximumBranches || receipt.Publications.Length!=receipt.Published.Length || !receipt.PublicationsCaptured && receipt.Publications.Length!=0
            || receipt.Publications.Any(publication=>publication.Id==Guid.Empty || !publication.EntityId.IsValid || publication.Snapshot.TradeId!=publication.EntityId))
            throw new InvalidDataException("Invalid captured option publication.");
        if(receipt.Commands.Select(command=>command.CommandId).Distinct().Count()!=receipt.Commands.Length)
            throw new InvalidDataException("Routing command identity collision.");
        foreach(var command in receipt.Commands)
            if(command.CommandId==Guid.Empty || command.ContractId!=Contract(receipt.Source) || command.SourceSequence!=Sequence(receipt.Source)
                || command.RouteGeneration<=0 || command.EffectiveAtUtc.Kind!=DateTimeKind.Utc || command.OptionTick != receipt.Source.OptionMarketPriceObservation?.OptionTickData)
                throw new InvalidDataException("Invalid captured routing command.");
    }
    void Forget(Guid id) { cache.Remove(id); if(nodes.Remove(id,out var node)) lru.Remove(node); }
    void Remember(Guid id,byte[] bytes)
    { Forget(id); cache.Add(id,bytes); nodes.Add(id,lru.AddLast(id)); if(root is not null) while(cache.Count>CacheLimit) Forget(lru.First!.Value); }
    public void Dispose()=>lease?.Dispose();
}
