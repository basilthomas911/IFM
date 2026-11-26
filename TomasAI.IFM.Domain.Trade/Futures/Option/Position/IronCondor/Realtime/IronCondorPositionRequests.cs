using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;

/// <summary>Original financial requests, committed before either Function dispatch.</summary>
public sealed record IronCondorPositionRequestReceipt
{
    public string SourceFingerprint { get; init; } = "";
    public CalculateIronCondorForwardPriceCommand Forward { get; init; } = new();
    public UpdateIronCondorTradePlanCommand Plan { get; init; } = new();
    public bool PlanFrozen { get; init; }
    public bool Completed { get; init; }
}

/// <summary>Checksummed, single-owner binary request journal. Completed captures stay on disk for source redelivery.</summary>
public sealed class IronCondorPositionRequests : IDisposable
{
    const int MaximumBytes = 8 * 1024 * 1024, CacheLimit = 128, PendingLimit = 4096;
    readonly Lock gate = new();
    readonly string? root;
    readonly FileStream? lease;
    readonly Dictionary<Guid, byte[]> cache = [];
    readonly LinkedList<Guid> cacheOrder = new();
    readonly Dictionary<Guid, LinkedListNode<Guid>> cacheNodes = [];
    readonly HashSet<Guid> pending = [];

    /// <summary>Null directory is explicitly volatile for isolated tests only. Production supplies persistent storage.</summary>
    public IronCondorPositionRequests(string? directory = null)
    {
        if (directory is null) { Publications = new(); return; }
        root = Path.GetFullPath(directory);
        AtomicDurableFile.EnsureDirectory(root);
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            AtomicDurableFile.EnsureDirectory(Path.Combine(root, "pending"));
            AtomicDurableFile.EnsureDirectory(Path.Combine(root, "receipts"));
            // Shard paths are fixed so file replacement never changes a request identity.
            for (var shard = 0; shard < 256; shard++)
            {
                var path = Path.Combine(root, "receipts", shard.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                AtomicDurableFile.EnsureDirectory(path);
            }
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.icr"))
            {
                var id = Guid.ParseExact(Path.GetFileNameWithoutExtension(file), "N");
                var receipt = Decode(Read(file));
                Validate(id, receipt);
                if (!receipt.Completed && !pending.Add(id) || pending.Count > PendingLimit)
                    throw new InvalidDataException("Invalid or excessive pending Iron Condor request captures.");
            }
            Publications = new(Path.Combine(root, "publications"));
        }
        catch { lease.Dispose(); throw; }
    }

    public IronCondorPlanPublications Publications { get; }

    string PendingPath(Guid id) => Path.Combine(root!, "pending", id.ToString("N") + ".icr");
    string DonePath(Guid id) => Path.Combine(root!, "receipts", id.ToString("N")[..2], id.ToString("N") + ".icr");
    public IronCondorPositionRequestReceipt? Find(Guid id, string sourceFingerprint)
    {
        lock (gate)
        {
            byte[]? bytes = cache.GetValueOrDefault(id);
            if (bytes is null && root is not null)
            {
                try { bytes = Read(PendingPath(id)); }
                catch (FileNotFoundException)
                {
                    try { bytes = Read(DonePath(id)); }
                    catch (FileNotFoundException) { return null; }
                }
            }
            if (bytes is null) return null;
            var receipt = Decode(bytes);
            Validate(id, receipt);
            if (!string.Equals(receipt.SourceFingerprint, sourceFingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("Iron Condor source identity was reused with different content.");
            if (!receipt.Completed && !pending.Contains(id))
            {
                if (pending.Count >= PendingLimit) throw new IOException("Iron Condor capture capacity exhausted.");
                pending.Add(id);
            }
            Remember(id, bytes);
            return receipt;
        }
    }

    public IronCondorPositionRequestReceipt Prepare(IronCondorPositionRequestReceipt proposed)
    {
        lock (gate)
        {
            var id = proposed.Forward.SourceEventId;
            var existing = Find(id, proposed.SourceFingerprint);
            if (existing is not null) return existing;
            if (proposed.PlanFrozen || proposed.Completed || pending.Count >= PendingLimit)
                throw new IOException("Iron Condor capture capacity exhausted or invalid initial stage; source must be retained.");
            Write(id, proposed);
            pending.Add(id);
            return Find(id, proposed.SourceFingerprint)!;
        }
    }

    public IronCondorPositionRequestReceipt FreezePlan(IronCondorPositionRequestReceipt capture, UpdateIronCondorTradePlanCommand plan)
    {
        lock (gate)
        {
            var id = capture.Forward.SourceEventId;
            var existing = Find(id, capture.SourceFingerprint) ?? throw new InvalidDataException("Missing request capture.");
            if (existing.PlanFrozen) return existing;
            // Only the Forward Price response/failure may be added to the frozen template.
            var expected = existing.Plan with { IronCondorForwardPriceCalculated = plan.IronCondorForwardPriceCalculated, ForwardPriceFailure = plan.ForwardPriceFailure };
            if (!MessagePackSerializer.Serialize(expected).AsSpan().SequenceEqual(MessagePackSerializer.Serialize(plan)))
                throw new InvalidDataException("Attempt to change frozen Iron Condor request inputs.");
            Write(id, existing with { Plan = plan, PlanFrozen = true });
            return Find(id, capture.SourceFingerprint)!;
        }
    }

    public void Complete(IronCondorPositionRequestReceipt capture)
    {
        lock (gate)
        {
            var id = capture.Forward.SourceEventId;
            var existing = Find(id, capture.SourceFingerprint) ?? throw new InvalidDataException("Missing request capture.");
            if (!existing.PlanFrozen) throw new InvalidDataException("Cannot complete an unprepared plan request.");
            if (!existing.Completed) Write(id, existing with { Completed = true });
            if (root is not null)
            {
                try { _ = File.GetAttributes(PendingPath(id)); }
                catch (FileNotFoundException) { pending.Remove(id); return; }
                using var mutation = RealtimeActorGeneration.EnterMutation();
                AtomicDurableFile.Replace(PendingPath(id), DonePath(id));
            }
            pending.Remove(id);
        }
    }

    void Write(Guid id, IronCondorPositionRequestReceipt receipt)
    {
        Validate(id, receipt);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1); writer.Write(receipt.SourceFingerprint); writer.Write(receipt.PlanFrozen); writer.Write(receipt.Completed);
            var forward = MessagePackSerializer.Serialize(receipt.Forward); writer.Write(forward.Length); writer.Write(forward);
            var plan = MessagePackSerializer.Serialize(receipt.Plan); writer.Write(plan.Length); writer.Write(plan);
        }
        var payload = buffer.ToArray();
        if (payload.Length > MaximumBytes - 32) throw new InvalidDataException("Iron Condor request capture exceeds its frame limit.");
        var bytes = new byte[32 + payload.Length];
        payload.CopyTo(bytes, 32); SHA256.HashData(payload, bytes.AsSpan(0, 32));
        using var mutation = RealtimeActorGeneration.EnterMutation();
        // Evict first: any ambiguous write/rename result must be reconciled from disk on retry.
        Forget(id);
        if (root is not null)
        {
            var path = PendingPath(id); var temporary = path + ".tmp";
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            AtomicDurableFile.Replace(temporary, path);
        }
        Remember(id, bytes);
    }

    void Forget(Guid id)
    {
        cache.Remove(id);
        if (cacheNodes.Remove(id, out var node)) cacheOrder.Remove(node);
    }
    void Remember(Guid id, byte[] bytes)
    {
        Forget(id); cache.Add(id, bytes); cacheNodes.Add(id, cacheOrder.AddLast(id));
        // Volatile test stores retain all captures so eviction cannot silently change replay.
        if (root is not null)
            while (cache.Count > CacheLimit) Forget(cacheOrder.First!.Value);
    }
    static byte[] Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 33 or > MaximumBytes) throw new InvalidDataException("Invalid Iron Condor request capture length.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); return bytes;
    }
    static IronCondorPositionRequestReceipt Decode(byte[] bytes)
    {
        if (bytes.Length is < 33 or > MaximumBytes || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(32)), bytes.AsSpan(0, 32)))
            throw new InvalidDataException("Iron Condor request capture checksum mismatch; recovery is blocked.");
        using var reader = new BinaryReader(new MemoryStream(bytes, 32, bytes.Length - 32, false), System.Text.Encoding.UTF8);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported Iron Condor request capture version.");
        var hash = reader.ReadString(); var frozen = reader.ReadBoolean(); var completed = reader.ReadBoolean();
        var forward = ReadCommand<CalculateIronCondorForwardPriceCommand>(reader);
        var plan = ReadCommand<UpdateIronCondorTradePlanCommand>(reader);
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing capture content.");
        return new() { SourceFingerprint = hash, Forward = forward, Plan = plan, PlanFrozen = frozen, Completed = completed };
    }
    static T ReadCommand<T>(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length is < 1 or > MaximumBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid captured command size.");
        return MessagePackSerializer.Deserialize<T>(reader.ReadBytes(length));
    }
    static void Validate(Guid id, IronCondorPositionRequestReceipt receipt)
    {
        if (id == Guid.Empty || receipt.SourceFingerprint.Length != 64 || receipt.Forward.SourceEventId != id || receipt.Plan.SourceEventId != id
            || receipt.Forward.CommandId == Guid.Empty || receipt.Plan.CommandId == Guid.Empty || receipt.Forward.EntityId != receipt.Plan.EntityId
            || receipt.Forward.RequestedAtUtc.Kind != DateTimeKind.Utc || receipt.Forward.RequestedAtUtc != receipt.Plan.RequestedAtUtc
            || receipt.Completed && !receipt.PlanFrozen)
            throw new InvalidDataException("Malformed Iron Condor request capture.");
    }
    public void Dispose() { Publications.Dispose(); lease?.Dispose(); }
}
