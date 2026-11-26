using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.RealtimePersistence;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Durable stages of an immutable TDI result. Ambiguous sends repeat the same event identity.</summary>
public enum TdiReceiptStage { Prepared, SourcePublished, Applied, Completed }

/// <summary>The original computed result and its original completion publication.</summary>
public sealed record FuturesTdiResultReceipt(
    string Fingerprint, FuturesTdiSignalGeneratedEvent Generated,
    FuturesTdiSignalGeneratedCompleteEvent Completed, TdiReceiptStage Stage)
{
    public FuturesTdiSignalEvaluation Evaluation => new(Generated.EntityId, Generated.FuturesTdiSignal, Generated);
}

/// <summary>Immutable binary receipts and a recoverable publication outbox. Completed history stays on disk, not in the hot cache.</summary>
public sealed class FuturesTdiResultReceipts : IDisposable
{
    const int MaximumRecordBytes = 64 * 1024, MaximumPending = 4096, CachedReceipts = 512;
    readonly Lock gate = new();
    readonly string? root;
    readonly FileStream? lease;
    readonly DurableJournalCapacity? capacity;
    public long MaximumRetainedBytes=>capacity?.MaximumBytes??0;
    public long RetainedBytes=>capacity?.RetainedBytes??0;
    public long SettlementReservedBytes=>capacity?.SettlementReservedBytes??0;
    readonly HashSet<Guid> pending = [];
    readonly Dictionary<Guid, (FuturesTdiResultReceipt Receipt, LinkedListNode<Guid> Node)> cache = [];
    readonly LinkedList<Guid> lru = new();

    /// <summary>A null directory is explicitly volatile for standalone tests; production DI supplies persistent storage.</summary>
    public FuturesTdiResultReceipts(string? directory = null):this(directory,DurableJournalCapacity.ConfiguredMaximum("IFM_TDI_RECEIPT_MAX_BYTES")){}
    public FuturesTdiResultReceipts(string? directory,long maximumRetainedBytes)
    {
        if (directory is null) return;
        root = Path.GetFullPath(directory);
        Directory.CreateDirectory(Path.Combine(root, "pending"));
        Directory.CreateDirectory(Path.Combine(root, "receipts"));
        lease = new FileStream(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            capacity=new(root,maximumRetainedBytes,MaximumRecordBytes);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.tdi"))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) || pending.Count >= MaximumPending)
                    throw new InvalidDataException("Invalid or excessive pending TDI receipts.");
                pending.Add(id);
            }
            foreach (var id in pending)
            {
                var receipt = Read(PendingPath(id));
                if (receipt.Generated.Id != id) throw new InvalidDataException("TDI pending receipt identity mismatch.");
                Remember(receipt);
            }
        }
        catch { lease.Dispose(); throw; }
    }

    string PendingPath(Guid id) => Path.Combine(root!, "pending", id.ToString("N") + ".tdi");
    string DonePath(Guid id) => Path.Combine(root!, "receipts", id.ToString("N")[..2], id.ToString("N") + ".tdi");

    public FuturesTdiResultReceipt? Find(Guid id)
    {
        lock (gate)
        {
            if (cache.TryGetValue(id, out var cached))
            { lru.Remove(cached.Node); lru.AddLast(cached.Node); return cached.Receipt; }
            if (root is null) return null;
            var path = DonePath(id);
            if (!Exists(path)) path = PendingPath(id);
            if (!Exists(path)) return null;
            var result = Read(path);
            if (result.Generated.Id != id) throw new InvalidDataException("TDI receipt identity mismatch.");
            Remember(result);
            return result;
        }
    }

    public FuturesTdiResultReceipt Prepare(FuturesTdiResultReceipt proposed)
    {
        lock (gate)
        {
            var existing = Find(proposed.Generated.Id);
            if (existing is not null)
            {
                if (existing.Fingerprint != proposed.Fingerprint) throw new InvalidDataException("A TDI window was replayed with conflicting inputs.");
                return existing;
            }
            if (pending.Count >= MaximumPending || root is null && cache.Count >= MaximumPending)
                throw new IOException("TDI receipt capacity exhausted; input must not be acknowledged.");
            using var mutation = RealtimeActorGeneration.EnterMutation();
            pending.Add(proposed.Generated.Id);
            try { Write(proposed); Remember(proposed); return proposed; }
            catch
            {
                Forget(proposed.Generated.Id);
                if (root is null || !Exists(PendingPath(proposed.Generated.Id))) pending.Remove(proposed.Generated.Id);
                throw;
            }
        }
    }

    public FuturesTdiResultReceipt Advance(Guid id, TdiReceiptStage stage)
    {
        lock (gate)
        {
            var original = Find(id) ?? throw new InvalidDataException("TDI publication has no prepared receipt.");
            if (stage < original.Stage) return original;
            if (stage == TdiReceiptStage.Completed && original.Stage == stage && !pending.Contains(id)) return original;
            using var mutation = RealtimeActorGeneration.EnterMutation();
            var updated = original with { Stage = stage };
            try
            {
                if (stage != original.Stage) Write(updated);
                if (stage == TdiReceiptStage.Completed)
                {
                    if (root is not null && Exists(PendingPath(id)))
                    {
                        var target = DonePath(id); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        capacity!.Move(PendingPath(id),target,()=>AtomicDurableFile.Replace(PendingPath(id), target));
                    }
                    pending.Remove(id);
                }
                Remember(updated); return updated;
            }
            catch { Forget(id); throw; }
        }
    }

    public FuturesTdiResultReceipt[] Pending()
    {
        lock (gate)
            return pending.Select(id => Find(id) ?? throw new InvalidDataException("Missing pending TDI receipt."))
                .OrderBy(receipt => receipt.Generated.FuturesTdiSignal.ValueDate)
                .ThenBy(receipt => receipt.Generated.FuturesTdiSignal.Timestamp)
                .ThenBy(receipt => receipt.Generated.FuturesTdiSignal.SourceSequence).ToArray();
    }

    void Remember(FuturesTdiResultReceipt receipt)
    {
        Forget(receipt.Generated.Id);
        cache.Add(receipt.Generated.Id, (receipt, lru.AddLast(receipt.Generated.Id)));
        if (root is not null && cache.Count > CachedReceipts) Forget(lru.First!.Value);
    }
    void Forget(Guid id) { if (cache.Remove(id, out var entry)) lru.Remove(entry.Node); }
    static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    void Write(FuturesTdiResultReceipt receipt)
    {
        if (root is null) return;
        using var buffer = new MemoryStream(4096);
        buffer.Write(new byte[32]);
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
        {
            writer.Write(1); writer.Write(receipt.Fingerprint); writer.Write((byte)receipt.Stage);
            WriteEvent(writer, receipt.Generated); WriteEvent(writer, receipt.Completed);
        }
        if (buffer.Length > MaximumRecordBytes) throw new InvalidDataException("TDI result receipt exceeds its frame bound.");
        var bytes = buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length));
        SHA256.HashData(bytes[32..], bytes[..32]);
        var path = PendingPath(receipt.Generated.Id); var temporary = path + ".tmp";
        var length=checked((int)buffer.Length);
        capacity!.Write(path,length,()=>
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(buffer.GetBuffer(),0,length); file.Flush(true); }
            AtomicDurableFile.Replace(temporary, path);
        });
    }
    static void WriteEvent<T>(BinaryWriter writer, T value) where T : class
    {
        var bytes = MessagePackSerializer.Serialize(value); writer.Write(bytes.Length); writer.Write(bytes);
        var origin = MessageOrigins.Get(value); writer.Write(origin is not null);
        if (origin is not null) writer.Write(MessageOriginBinary.Serialize(origin));
    }
    static T ReadEvent<T>(BinaryReader reader) where T : class
    {
        var size = reader.ReadInt32();
        if (size is < 1 or > MaximumRecordBytes) throw new InvalidDataException("Invalid TDI event frame size.");
        var bytes = reader.ReadBytes(size); if (bytes.Length != size) throw new EndOfStreamException();
        var value = MessagePackSerializer.Deserialize<T>(bytes);
        return MessageOrigins.Restore(value, reader.ReadBoolean() ? MessageOriginBinary.Deserialize(reader.ReadString()) : null);
    }
    static FuturesTdiResultReceipt Read(string path)
    {
        var size = new FileInfo(path).Length;
        if (size is < 36 or > MaximumRecordBytes) throw new InvalidDataException("Invalid TDI receipt length.");
        var bytes = File.ReadAllBytes(path);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(32)), bytes.AsSpan(0, 32)))
            throw new InvalidDataException("TDI receipt checksum mismatch; recovery is blocked.");
        using var reader = new BinaryReader(new MemoryStream(bytes, 32, bytes.Length - 32, false), Encoding.UTF8);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported TDI receipt version.");
        var hash = reader.ReadString(); var stage = (TdiReceiptStage)reader.ReadByte();
        var generated = ReadEvent<FuturesTdiSignalGeneratedEvent>(reader);
        var completed = ReadEvent<FuturesTdiSignalGeneratedCompleteEvent>(reader);
        if (!Enum.IsDefined(stage) || hash.Length != 64 || generated.Id == Guid.Empty || completed.Id == Guid.Empty
            || generated.CommandId != completed.CommandId || generated.EntityId != completed.EntityId
            || reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Malformed TDI result receipt.");
        return new(hash, generated, completed, stage);
    }

    public void Dispose() => lease?.Dispose();
}
