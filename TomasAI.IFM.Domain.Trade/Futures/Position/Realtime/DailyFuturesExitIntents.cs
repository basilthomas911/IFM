using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;

public enum DailyExitIntentStage : byte { Pending, Confirmed, Retired }
public sealed record DailyExitIntent(Guid Key, StartFuturesExitPositionWorkflowCommand Command, DailyExitIntentStage Stage, string Reason = "");

/// <summary>Single-host exclusive, immutable first exit authority per position generation. No automatic history pruning.</summary>
public sealed class DailyFuturesExitIntents : IDisposable
{
    const int MaximumBytes = 8 * 1024 * 1024, PendingLimit = 4096, CacheLimit = 128;
    readonly Lock gate = new();
    readonly string? root;
    readonly FileStream? lease;
    readonly HashSet<Guid> pending = [];
    readonly Dictionary<Guid, byte[]> cache = [];
    readonly LinkedList<Guid> lru = new();
    readonly Dictionary<Guid, LinkedListNode<Guid>> nodes = [];
    /// <summary>Null storage is explicitly volatile for isolated tests only.</summary>
    public DailyFuturesExitIntents(string? directory = null)
    {
        if (directory is null) return;
        root = Path.GetFullPath(directory); AtomicDurableFile.EnsureDirectory(root);
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            AtomicDurableFile.EnsureDirectory(Path.Combine(root, "pending"));
            AtomicDurableFile.EnsureDirectory(Path.Combine(root, "history"));
            for (var i = 0; i < 256; i++) AtomicDurableFile.EnsureDirectory(Path.Combine(root, "history", i.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.dfe"))
            {
                var key = Guid.ParseExact(Path.GetFileNameWithoutExtension(file), "N");
                Validate(key, Decode(Read(file)));
                if (pending.Count >= PendingLimit) throw new IOException("Daily exit pending intent capacity exhausted.");
                pending.Add(key);
            }
        }
        catch { lease.Dispose(); throw; }
    }
    public static Guid Key(StrategyPositionSnapshot position) => TradePlanContractIdentity.DeterministicId(
        FormattableString.Invariant($"daily-exit-intent/v1|{position.Id.Format()}|{position.RouteGeneration}"));
    string PendingPath(Guid key) => Path.Combine(root!, "pending", key.ToString("N") + ".dfe");
    string HistoryPath(Guid key) => Path.Combine(root!, "history", key.ToString("N")[..2], key.ToString("N") + ".dfe");
    DailyExitIntent? FindKey(Guid key)
    {
        var bytes = cache.GetValueOrDefault(key);
        if (bytes is null && root is not null)
        {
            try { bytes = Read(PendingPath(key)); }
            catch (FileNotFoundException)
            {
                try { bytes = Read(HistoryPath(key)); }
                catch (FileNotFoundException) { return null; }
            }
        }
        if (bytes is null) return null;
        var receipt = Decode(bytes); Validate(key, receipt); Remember(key, bytes); return receipt;
    }
    public DailyExitIntent? Find(StrategyPositionSnapshot position)
    {
        lock (gate)
        {
            var found = FindKey(Key(position));
            if (found is not null)
            {
                var original = found.Command.ExitPlan.Plan.Position;
                if (position.Id != original.Id || position.DailyFuturesEntry?.DcTriggerId != original.DailyFuturesEntry!.DcTriggerId)
                    throw new InvalidDataException("Daily exit generation reused with different entry identity.");
            }
            return found;
        }
    }
    public DailyExitIntent Claim(StartFuturesExitPositionWorkflowCommand command)
    {
        lock (gate)
        {
            var key = Key(command.ExitPlan.Plan.Position);
            var proposed = new DailyExitIntent(key, command, DailyExitIntentStage.Pending); Validate(key, proposed);
            var original = Find(command.ExitPlan.Plan.Position);
            if (original is not null) return original; // First authority wins, including later valid exit plans.
            if (pending.Count >= PendingLimit) throw new IOException("Daily exit pending intent capacity exhausted; retain source.");
            pending.Add(key);
            try { Write(proposed); }
            catch
            {
                if (root is null) pending.Remove(key);
                else
                    try { _ = File.GetAttributes(PendingPath(key)); }
                    catch (FileNotFoundException) { pending.Remove(key); }
                throw;
            }
            return FindKey(key)!;
        }
    }
    public void Finish(DailyExitIntent original, DailyExitIntentStage stage, string reason = "")
    {
        lock (gate)
        {
            if (stage == DailyExitIntentStage.Pending) throw new ArgumentOutOfRangeException(nameof(stage));
            var existing = FindKey(original.Key) ?? throw new InvalidDataException("Missing Daily exit intent.");
            if (existing.Stage != DailyExitIntentStage.Pending && !pending.Contains(original.Key)) return;
            if (existing.Stage == DailyExitIntentStage.Pending) Write(existing with { Stage = stage, Reason = reason });
            if (root is not null)
            {
                try { _ = File.GetAttributes(PendingPath(original.Key)); }
                catch (FileNotFoundException) { pending.Remove(original.Key); return; }
                using var mutation = RealtimeActorGeneration.EnterMutation();
                AtomicDurableFile.Replace(PendingPath(original.Key), HistoryPath(original.Key));
            }
            pending.Remove(original.Key);
        }
    }
    public DailyExitIntent[] Pending()
    {
        lock (gate) return pending.Select(key => FindKey(key) ?? throw new InvalidDataException("Missing pending Daily exit."))
            .OrderBy(item => item.Command.ExitPlan.Plan.ValueDate).ThenBy(item => item.Command.EntityId.Position.Format(), StringComparer.Ordinal).ToArray();
    }
    void Write(DailyExitIntent intent)
    {
        Validate(intent.Key, intent);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1); writer.Write(intent.Key.ToByteArray()); writer.Write((byte)intent.Stage); writer.Write(intent.Reason);
            var command = MessagePackSerializer.Serialize(intent.Command); writer.Write(command.Length); writer.Write(command);
            var origin = MessageOrigins.Get(intent.Command); writer.Write(origin is not null);
            if (origin is not null) writer.Write(MessageOriginBinary.Serialize(origin));
        }
        if (buffer.Length > MaximumBytes - 32) throw new InvalidDataException("Daily exit intent exceeds frame limit.");
        var payload = buffer.GetBuffer().AsSpan(0, (int)buffer.Length); var frame = new byte[32 + payload.Length];
        payload.CopyTo(frame.AsSpan(32)); SHA256.HashData(payload, frame.AsSpan(0, 32));
        using var mutation = RealtimeActorGeneration.EnterMutation(); Forget(intent.Key);
        if (root is not null)
        {
            var file = PendingPath(intent.Key); var temporary = file + ".tmp";
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { output.Write(frame); output.Flush(true); }
            AtomicDurableFile.Replace(temporary, file);
        }
        Remember(intent.Key, frame);
    }
    static byte[] Read(string file)
    {
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < 33 or > MaximumBytes) throw new InvalidDataException("Invalid Daily exit intent length.");
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }
    static DailyExitIntent Decode(byte[] frame)
    {
        if (frame.Length is < 33 or > MaximumBytes || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(frame.AsSpan(32)), frame.AsSpan(0, 32)))
            throw new InvalidDataException("Daily exit intent checksum mismatch; recovery blocked.");
        using var reader = new BinaryReader(new MemoryStream(frame, 32, frame.Length - 32, false), System.Text.Encoding.UTF8);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported Daily exit intent version.");
        var key = new Guid(reader.ReadBytes(16)); var stage = (DailyExitIntentStage)reader.ReadByte(); var reason = reader.ReadString(); var length = reader.ReadInt32();
        if (length is < 1 or > MaximumBytes || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid exit command size.");
        var command = MessagePackSerializer.Deserialize<StartFuturesExitPositionWorkflowCommand>(reader.ReadBytes(length));
        command = MessageOrigins.Restore(command, reader.ReadBoolean() ? MessageOriginBinary.Deserialize(reader.ReadString()) : null);
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing Daily exit intent bytes.");
        return new(key, command, stage, reason);
    }
    static void Validate(Guid key, DailyExitIntent intent)
    {
        var command = intent.Command; var plan = command.ExitPlan.Plan; var position = plan.Position;
        if (key == Guid.Empty || intent.Key != key || Key(position) != key || !Enum.IsDefined(intent.Stage)
            || command.ExitPlan.Id == Guid.Empty || !position.IsOpen || position.RouteGeneration <= 0 || position.DailyFuturesEntry is not { DcTriggerId: var trigger }
            || trigger == Guid.Empty || position.StrategyKind != TradeStrategyKind.FuturesOutright || !plan.RequiresExit || plan.State != TradePlanState.ExitRequired
            || plan.Action is not (TradePlanAction.ExitAtMarket or TradePlanAction.ExitAtLimit) || command.EntityId.Position != position.Id
            || command.EntityId.ValueDate != plan.ValueDate || command.CommandId != TradePlanContractIdentity.DeterministicId($"{command.EntityId.Format()}|{StartFuturesExitPositionWorkflowCommand.Verb}"))
            throw new InvalidDataException("Invalid Daily exit intent authority.");
    }
    void Forget(Guid key) { cache.Remove(key); if (nodes.Remove(key, out var node)) lru.Remove(node); }
    void Remember(Guid key, byte[] frame)
    {
        Forget(key); cache.Add(key, frame); nodes.Add(key, lru.AddLast(key));
        if (root is not null) while (cache.Count > CacheLimit) Forget(lru.First!.Value);
    }
    public void Dispose() => lease?.Dispose();
}
