using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Original Market Condition initialization-failure command, frozen before its first dispatch.</summary>
public sealed record WorkflowMarketConditionFailure(Guid Key, string InputHash, FailMarketConditionCommand Command, bool Sent);

/// <summary>Single-owner binary terminal journal. Confirmation requires the command owner's successful reply.</summary>
public sealed class WorkflowMarketConditionFailures : IDisposable
{
    const int MaximumBytes = 8 * 1024 * 1024, PendingLimit = 4096, CacheLimit = 128;
    readonly Lock gate = new();
    readonly string? root;
    readonly FileStream? lease;
    readonly Dictionary<Guid, byte[]> cached = [];
    readonly LinkedList<Guid> lru = new();
    readonly Dictionary<Guid, LinkedListNode<Guid>> nodes = [];
    readonly Dictionary<Guid, IntrinsicTimeStrategyWorkflowEntityId> pending = [];

    public WorkflowMarketConditionFailures(string? directory = null)
    {
        if (directory is null) return; // Explicitly volatile isolated tests only.
        root = Path.GetFullPath(directory); AtomicDurableFile.EnsureDirectory(root);
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            AtomicDurableFile.EnsureDirectory(Path.Combine(root, "pending"));
            AtomicDurableFile.EnsureDirectory(Path.Combine(root, "sent"));
            for (var shard = 0; shard < 256; shard++)
                AtomicDurableFile.EnsureDirectory(Path.Combine(root, "sent", shard.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.wmf"))
            {
                var key = Guid.ParseExact(Path.GetFileNameWithoutExtension(file), "N");
                var receipt = Decode(Read(file)); Validate(key, receipt);
                if (pending.Count >= PendingLimit) throw new IOException("Workflow Market Condition initialization pending terminal capacity exhausted.");
                pending.Add(key, receipt.Command.EntityId);
            }
        }
        catch { lease.Dispose(); throw; }
    }

    string PendingPath(Guid key) => Path.Combine(root!, "pending", key.ToString("N") + ".wmf");
    string SentPath(Guid key) => Path.Combine(root!, "sent", key.ToString("N")[..2], key.ToString("N") + ".wmf");
    public WorkflowMarketConditionFailure? Find(Guid key)
    {
        lock (gate)
        {
            var bytes = cached.GetValueOrDefault(key);
            if (bytes is null && root is not null)
            {
                try { bytes = Read(PendingPath(key)); }
                catch (FileNotFoundException)
                {
                    try { bytes = Read(SentPath(key)); }
                    catch (FileNotFoundException) { return null; }
                }
            }
            if (bytes is null) return null;
            var receipt = Decode(bytes); Validate(key, receipt); Remember(key, bytes); return receipt;
        }
    }

    public WorkflowMarketConditionFailure Prepare(Guid key, string inputHash, Func<FailMarketConditionCommand> create)
    {
        lock (gate)
        {
            var existing = Find(key);
            if (existing is not null)
            {
                if (!string.Equals(existing.InputHash, inputHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Workflow Market Condition initialization terminal identity reused with different input.");
                return existing;
            }
            if (pending.Count >= PendingLimit) throw new IOException("Workflow Market Condition initialization pending terminal capacity exhausted; retain input.");
            var receipt = new WorkflowMarketConditionFailure(key, inputHash, create(), false);
            pending.Add(key, receipt.Command.EntityId);
            try { Write(receipt); }
            catch
            {
                if (root is null) pending.Remove(key);
                else
                {
                    // An ambiguous durable rename may already have committed the record.
                    try { _ = File.GetAttributes(PendingPath(key)); }
                    catch (FileNotFoundException) { pending.Remove(key); }
                    catch (DirectoryNotFoundException) { pending.Remove(key); }
                }
                throw;
            }
            return Find(key)!;
        }
    }

    public void MarkSent(Guid key)
    {
        lock (gate)
        {
            var original = Find(key) ?? throw new InvalidDataException("Publication was not prepared.");
            if (!original.Sent) Write(original with { Sent = true });
            if (root is not null)
            {
                try { _ = File.GetAttributes(PendingPath(key)); }
                catch (FileNotFoundException) { pending.Remove(key); return; }
                using var mutation = RealtimeActorGeneration.EnterMutation();
                AtomicDurableFile.Replace(PendingPath(key), SentPath(key));
            }
            pending.Remove(key);
        }
    }

    public WorkflowMarketConditionFailure[] Pending(IntrinsicTimeStrategyWorkflowEntityId? entity = null)
    {
        lock (gate)
            return pending.Where(pair => entity is null || pair.Value == entity.Value)
                .Select(pair => Find(pair.Key) ?? throw new InvalidDataException("Missing pending publication."))
                .OrderBy(receipt => receipt.Command.FailedAtUtc)
                .ThenBy(receipt => receipt.Command.InputWorkflowRevision).ToArray();
    }

    void Write(WorkflowMarketConditionFailure receipt)
    {
        Validate(receipt.Key, receipt);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1); writer.Write(receipt.Key.ToByteArray()); writer.Write(receipt.InputHash); writer.Write(receipt.Sent);
            var bytes = MessagePackSerializer.Serialize(receipt.Command); writer.Write(bytes.Length); writer.Write(bytes);
            var origin = MessageOrigins.Get(receipt.Command); writer.Write(origin is not null);
            if (origin is not null) writer.Write(MessageOriginBinary.Serialize(origin));
        }
        if (buffer.Length > MaximumBytes - 32) throw new InvalidDataException("Workflow Market Condition initialization terminal exceeds frame limit.");
        var payload = buffer.GetBuffer().AsSpan(0, (int)buffer.Length); var frame = new byte[32 + payload.Length];
        payload.CopyTo(frame.AsSpan(32)); SHA256.HashData(payload, frame.AsSpan(0, 32));
        using var mutation = RealtimeActorGeneration.EnterMutation();
        Forget(receipt.Key);
        if (root is not null)
        {
            var path = PendingPath(receipt.Key); var temporary = path + ".tmp";
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(frame); file.Flush(true); }
            AtomicDurableFile.Replace(temporary, path);
        }
        Remember(receipt.Key, frame);
    }
    static byte[] Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 33 or > MaximumBytes) throw new InvalidDataException("Invalid publication frame length.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); return bytes;
    }
    static WorkflowMarketConditionFailure Decode(byte[] bytes)
    {
        if (bytes.Length is < 33 or > MaximumBytes || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(32)), bytes.AsSpan(0, 32)))
            throw new InvalidDataException("Workflow Market Condition initialization terminal checksum mismatch; recovery blocked.");
        using var reader = new BinaryReader(new MemoryStream(bytes, 32, bytes.Length - 32, false), System.Text.Encoding.UTF8);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported publication version.");
        var key = new Guid(reader.ReadBytes(16)); var hash = reader.ReadString(); var sent = reader.ReadBoolean(); var size = reader.ReadInt32();
        if (size is < 1 or > MaximumBytes || size > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid publication payload size.");
        var value = MessagePackSerializer.Deserialize<FailMarketConditionCommand>(reader.ReadBytes(size));
        value = MessageOrigins.Restore(value, reader.ReadBoolean() ? MessageOriginBinary.Deserialize(reader.ReadString()) : null);
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing publication frame bytes.");
        return new(key, hash, value, sent);
    }
    static void Validate(Guid key, WorkflowMarketConditionFailure receipt)
    {
        if (key == Guid.Empty || receipt.Key != key || receipt.InputHash.Length != 64 || receipt.Command.CommandId == Guid.Empty
            || receipt.Command.SourceEventId != key || string.IsNullOrWhiteSpace(receipt.Command.EntityId.ItiSignalEntityId.ContractId) || receipt.Command.InputWorkflowRevision <= 0
            || receipt.Command.FailedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("Malformed Workflow Market Condition initialization terminal.");
    }
    void Forget(Guid key) { cached.Remove(key); if (nodes.Remove(key, out var node)) lru.Remove(node); }
    void Remember(Guid key, byte[] bytes)
    {
        Forget(key); cached.Add(key, bytes); nodes.Add(key, lru.AddLast(key));
        if (root is not null) while (cached.Count > CacheLimit) Forget(lru.First!.Value);
    }
    public void Dispose() => lease?.Dispose();
}
