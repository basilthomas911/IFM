using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Original regime Function terminal command (legacy type name retained), frozen before its first dispatch.</summary>
public sealed record WorkflowRegimeFunctionFailure(Guid Key, string InputHash, ICommand<IntrinsicTimeStrategyWorkflowEntityId> Command, bool Sent);

/// <summary>Single-owner binary terminal journal. Confirmation requires the command owner's successful reply.</summary>
public sealed class WorkflowRegimeFunctionFailures : IDisposable
{
    const int MaximumBytes = 8 * 1024 * 1024, PendingLimit = 4096, CacheLimit = 128;
    readonly Lock gate = new();
    readonly string? root;
    readonly FileStream? lease;
    readonly Dictionary<Guid, byte[]> cached = [];
    readonly LinkedList<Guid> lru = new();
    readonly Dictionary<Guid, LinkedListNode<Guid>> nodes = [];
    readonly Dictionary<Guid, IntrinsicTimeStrategyWorkflowEntityId> pending = [];

    public WorkflowRegimeFunctionFailures(string? directory = null)
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
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.wgf"))
            {
                var key = Guid.ParseExact(Path.GetFileNameWithoutExtension(file), "N");
                var receipt = Decode(Read(file)); Validate(key, receipt);
                if (pending.Count >= PendingLimit) throw new IOException("Workflow regime Function pending terminal capacity exhausted.");
                pending.Add(key, receipt.Command.EntityId);
            }
        }
        catch { lease.Dispose(); throw; }
    }

    string PendingPath(Guid key) => Path.Combine(root!, "pending", key.ToString("N") + ".wgf");
    string SentPath(Guid key) => Path.Combine(root!, "sent", key.ToString("N")[..2], key.ToString("N") + ".wgf");
    public WorkflowRegimeFunctionFailure? Find(Guid key)
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

    public WorkflowRegimeFunctionFailure Prepare(Guid key, string inputHash, Func<ICommand<IntrinsicTimeStrategyWorkflowEntityId>> create)
    {
        lock (gate)
        {
            var existing = Find(key);
            if (existing is not null)
            {
                if (!string.Equals(existing.InputHash, inputHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Workflow regime Function terminal identity reused with different input.");
                return existing;
            }
            if (pending.Count >= PendingLimit) throw new IOException("Workflow regime Function pending terminal capacity exhausted; retain input.");
            var receipt = new WorkflowRegimeFunctionFailure(key, inputHash, create(), false);
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

    public WorkflowRegimeFunctionFailure[] Pending(IntrinsicTimeStrategyWorkflowEntityId? entity = null)
    {
        lock (gate)
            return pending.Where(pair => entity is null || pair.Value == entity.Value)
                .Select(pair => Find(pair.Key) ?? throw new InvalidDataException("Missing pending publication."))
                .OrderBy(receipt => Time(receipt.Command))
                .ThenBy(receipt => Revision(receipt.Command)).ToArray();
    }

    void Write(WorkflowRegimeFunctionFailure receipt)
    {
        Validate(receipt.Key, receipt);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
        {
            writer.Write(2); writer.Write(receipt.Key.ToByteArray()); writer.Write(receipt.InputHash); writer.Write(receipt.Sent);
            byte kind; byte[] bytes;
            switch(receipt.Command)
            {
                case FailRegimeDiscoveryCommand failure:kind=1;bytes=MessagePackSerializer.Serialize(failure);break;
                case CompleteRegimeDiscoveryCommand completion:kind=2;bytes=MessagePackSerializer.Serialize(completion);break;
                default:throw new InvalidDataException("Unsupported Regime Function terminal kind.");
            }
            writer.Write(kind);writer.Write(bytes.Length);writer.Write(bytes);
            var origin = MessageOrigins.Get(receipt.Command); writer.Write(origin is not null);
            if (origin is not null) writer.Write(MessageOriginBinary.Serialize(origin));
        }
        if (buffer.Length > MaximumBytes - 32) throw new InvalidDataException("Workflow regime Function terminal exceeds frame limit.");
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
    static WorkflowRegimeFunctionFailure Decode(byte[] bytes)
    {
        if (bytes.Length is < 33 or > MaximumBytes || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(32)), bytes.AsSpan(0, 32)))
            throw new InvalidDataException("Workflow regime Function terminal checksum mismatch; recovery blocked.");
        using var reader = new BinaryReader(new MemoryStream(bytes, 32, bytes.Length - 32, false), System.Text.Encoding.UTF8);
        var version=reader.ReadInt32();if(version is not (1 or 2))throw new InvalidDataException("Unsupported publication version.");
        var key = new Guid(reader.ReadBytes(16)); var hash = reader.ReadString(); var sent = reader.ReadBoolean(); var kind=version==1?(byte)1:reader.ReadByte(); var size = reader.ReadInt32();
        if (size is < 1 or > MaximumBytes || size > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid publication payload size.");
        var payload=reader.ReadBytes(size);
        ICommand<IntrinsicTimeStrategyWorkflowEntityId> value=kind switch
        {
            1=>MessagePackSerializer.Deserialize<FailRegimeDiscoveryCommand>(payload),
            2=>MessagePackSerializer.Deserialize<CompleteRegimeDiscoveryCommand>(payload),
            _=>throw new InvalidDataException("Unknown Regime Function terminal kind.")
        };
        value = MessageOrigins.Restore(value, reader.ReadBoolean() ? MessageOriginBinary.Deserialize(reader.ReadString()) : null);
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing publication frame bytes.");
        return new(key, hash, value, sent);
    }
    static DateTime Time(ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)=>command switch
    {FailRegimeDiscoveryCommand c=>c.FailedAtUtc,CompleteRegimeDiscoveryCommand c=>c.CompletedAtUtc,_=>default};
    static long Revision(ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)=>command switch
    {FailRegimeDiscoveryCommand c=>c.InputWorkflowRevision,CompleteRegimeDiscoveryCommand c=>c.InputWorkflowRevision,_=>0};
    static void Validate(Guid key, WorkflowRegimeFunctionFailure receipt)
    {
        var valid=receipt.Command switch
        {
            FailRegimeDiscoveryCommand c=>ValidIdentity(key,c.WorkflowId,c.InputWorkflowRevision,c.SourceEventId,c.CausationId),
            CompleteRegimeDiscoveryCommand c=>ValidIdentity(key,c.WorkflowId,c.InputWorkflowRevision,c.SourceEventId,c.CausationId) && c.Result is not null,
            _=>false
        };
        if(!valid||key==Guid.Empty||receipt.Key!=key||receipt.InputHash.Length!=64||receipt.Command.CommandId==Guid.Empty
            ||string.IsNullOrWhiteSpace(receipt.Command.EntityId.ItiSignalEntityId.ContractId)||Time(receipt.Command).Kind!=DateTimeKind.Utc)
            throw new InvalidDataException("Malformed Workflow Regime Function terminal.");
    }
    static bool ValidIdentity(Guid key,StrategyWorkflowId workflow,long revision,Guid source,Guid causation)=>revision>0 && source!=Guid.Empty && source==causation
        && key==Actor.IntrinsicTimeStrategyWorkflowRealtimeActor.DeterministicPipelineCommandId(workflow,Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStage.RegimeDiscovery,revision);
    void Forget(Guid key) { cached.Remove(key); if (nodes.Remove(key, out var node)) lru.Remove(node); }
    void Remember(Guid key, byte[] bytes)
    {
        Forget(key); cached.Add(key, bytes); nodes.Add(key, lru.AddLast(key));
        if (root is not null) while (cached.Count > CacheLimit) Forget(lru.First!.Value);
    }
    public void Dispose() => lease?.Dispose();
}
