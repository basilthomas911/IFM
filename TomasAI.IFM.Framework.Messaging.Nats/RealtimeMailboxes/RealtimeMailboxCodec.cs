using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

internal sealed record CapturedRealtimeSource(string Key, RealtimeSourcePublication Source, ActorSubject[] Destinations,
    string SourceHash, string ManifestHash, bool ArchiveOwnershipOnly = false, DateTimeOffset? RecordedAtUtc = null)
{
    public RealtimeCaptureReceipt Receipt(bool duplicate) => new(Source.SourceEventId, SourceHash, ManifestHash, Destinations.Length, duplicate);
}

internal static class RealtimeMailboxCodec
{
    public static string MailboxKey(ActorThreadId identity)
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        { writer.Write((int)identity.ActorType); writer.Write(identity.Name); writer.Write(identity.EntityId); }
        return Convert.ToHexStringLower(SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length)));
    }
    public static string ActorKey(string actor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(actor)))[..16];
    public static CapturedRealtimeSource Capture(RealtimeSourcePublication source, ActorSubject[] destinations, int maximumPayload)
        => CaptureCore(source, destinations, maximumPayload, false);
    internal static CapturedRealtimeSource CaptureArchive(RealtimeSourcePublication source, ActorSubject[] destinations, int maximumPayload)
        => CaptureCore(source, destinations, maximumPayload, true);
    static CapturedRealtimeSource CaptureCore(RealtimeSourcePublication source, ActorSubject[] destinations, int maximumPayload, bool archiveOnly)
    {
        if (source.Subject.ActorType != ActorType.Realtime) throw new InvalidDataException("Realtime source uses another actor category.");
        if (source.SourceEventId == Guid.Empty) throw new InvalidDataException("Realtime publication has no immutable event ID: " + source.Subject.Name + "." + source.Subject.Verb);
        if (source.Payload.IsEmpty || source.Payload.Length > maximumPayload)
            throw new InvalidDataException($"Realtime payload bytes {source.Payload.Length} outside 1..{maximumPayload}: {source.Subject.Name}.{source.Subject.Verb}");
        if (destinations.Length > 256) throw new InvalidDataException("Realtime manifest exceeds bounds.");
        ValidateSubject(source.Subject);
        foreach (var destination in destinations) ValidateSubject(destination, archiveOnly);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var header = new MemoryStream();
        using (var writer = new BinaryWriter(header, Encoding.UTF8, true))
        { WriteSubject(writer, source.Subject); writer.Write(source.SourceEventId.ToByteArray()); writer.Write(source.Dataset ?? ""); writer.Write(source.SourceGenerationId.ToByteArray()); }
        using var identity = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        identity.AppendData(header.GetBuffer().AsSpan(0, (int)header.Length));
        if (source.Origin is { } scope) identity.AppendData(Encoding.UTF8.GetBytes(scope.ClockDomainId + "|" + scope.RunId));
        hash.AppendData(header.GetBuffer().AsSpan(0, (int)header.Length));
        hash.AppendData(Encoding.UTF8.GetBytes(source.Origin is { } original ? MessageOriginBinary.Serialize(original) : "unknown"));
        hash.AppendData(source.Payload.Span);
        var sourceHash = Convert.ToHexStringLower(hash.GetHashAndReset());
        // Clock events are reconstructable wakeups, not immutable market facts; each distinct captured body remains explicit.
        if (source.Subject.Verb == "EvaluateFuturesExitClock") identity.AppendData(Convert.FromHexString(sourceHash));
        var key = Convert.ToHexStringLower(identity.GetHashAndReset());
        using var manifest = new MemoryStream();
        using (var writer = new BinaryWriter(manifest, Encoding.UTF8, true)) foreach (var target in destinations) WriteSubject(writer, target);
        if (manifest.Length > 32768) throw new InvalidDataException("Realtime manifest exceeds byte bounds.");
        var manifestHash = Convert.ToHexStringLower(SHA256.HashData(manifest.GetBuffer().AsSpan(0, (int)manifest.Length)));
        // Body is copied once here; caller-owned/pooled source memory may be released after capture returns.
        return new(key, source with { Payload = source.Payload.ToArray() }, destinations.ToArray(), sourceHash, manifestHash, archiveOnly);
    }
    public static byte[] Encode(CapturedRealtimeSource value)
    {
        // Keep only bounded metadata in the temporary writer. The immutable
        // large body is copied directly into its one exact final wire array.
        using var memory = new MemoryStream(512);
        using var writer = new BinaryWriter(memory, Encoding.UTF8, true);
        var archiveArrival = value.ArchiveOwnershipOnly && value.RecordedAtUtc is not null;
        writer.Write(archiveArrival ? 4 : value.ArchiveOwnershipOnly ? 3 : 2); writer.Write(value.Key); writer.Write(value.SourceHash); writer.Write(value.ManifestHash);
        WriteSubject(writer, value.Source.Subject); writer.Write(value.Source.SourceEventId.ToByteArray());
        writer.Write(value.Source.Dataset ?? ""); writer.Write(value.Source.SourceGenerationId.ToByteArray());
        writer.Write(value.Source.TraceParent ?? ""); writer.Write(value.Source.TraceState ?? "");
        writer.Write(value.Source.Origin is not null);
        if (value.Source.Origin is { } origin) writer.Write(MessageOriginBinary.Serialize(origin));
        writer.Write(value.Destinations.Length); foreach (var target in value.Destinations) WriteSubject(writer, target);
        if (archiveArrival) writer.Write(value.RecordedAtUtc!.Value.UtcTicks);
        writer.Write(value.Source.Payload.Length); writer.Flush();
        if (memory.Length > 65536) throw new InvalidDataException("Realtime envelope metadata exceeds reserved byte bound.");
        var headerLength = checked((int)memory.Length);
        var bytes = new byte[checked(headerLength + value.Source.Payload.Length)];
        memory.GetBuffer().AsSpan(0, headerLength).CopyTo(bytes);
        value.Source.Payload.Span.CopyTo(bytes.AsSpan(headerLength));
        return bytes;
    }
    public static CapturedRealtimeSource Decode(byte[] bytes, int maximumPayload, bool archiveOnly = false)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
        var version = reader.ReadInt32();
        if (archiveOnly ? version is not (3 or 4) : version != 2) throw new InvalidDataException("Unknown or incompatible realtime/archive envelope version.");
        var key = reader.ReadString(); var sourceHash = reader.ReadString(); var manifestHash = reader.ReadString();
        var source = ReadSubject(reader); var id = new Guid(ReadExactly(reader, 16));
        var dataset = reader.ReadString(); var generation = new Guid(ReadExactly(reader, 16));
        var traceParent = reader.ReadString(); var traceState = reader.ReadString();
        if (traceParent.Length > 256 || traceState.Length > 1024) throw new InvalidDataException("Invalid realtime trace metadata.");
        var origin = reader.ReadBoolean() ? MessageOriginBinary.Deserialize(reader.ReadString()) : null;
        var count = reader.ReadInt32(); if (count is < 0 or > 256) throw new InvalidDataException("Realtime manifest exceeds bounds.");
        var targets = new ActorSubject[count]; for (var i = 0; i < count; i++) targets[i] = ReadSubject(reader, archiveOnly);
        DateTimeOffset? recordedAt = version == 4 ? new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero) : null;
        var length = reader.ReadInt32(); if (length <= 0 || length > maximumPayload) throw new InvalidDataException("Invalid realtime payload length.");
        var offset = checked((int)reader.BaseStream.Position);
        if (offset > 65536) throw new InvalidDataException("Realtime envelope metadata exceeds reserved byte bound.");
        var remaining = reader.BaseStream.Length - offset;
        if (remaining < length) throw new EndOfStreamException();
        if (remaining > length) throw new InvalidDataException("Trailing realtime envelope bytes.");
        // Capture still defensively copies exactly once. This slice is never
        // retained, so callers may release/mutate their wire after Decode returns.
        var body = bytes.AsMemory(offset, length);
        var captured = CaptureCore(new(source, id, body, origin, string.IsNullOrEmpty(dataset) ? null : dataset, generation, traceParent, traceState), targets, maximumPayload, archiveOnly);
        if (captured.Key != key || captured.SourceHash != sourceHash || captured.ManifestHash != manifestHash) throw new InvalidDataException("Realtime envelope identity mismatch.");
        return captured with { RecordedAtUtc = recordedAt };
    }
    public static byte[] Delivery(CapturedRealtimeSource source, ActorSubject target) => source.Destinations.Contains(target) ? Encode(source) : throw new InvalidDataException("Destination not in captured manifest.");
    public static void WriteSubject(BinaryWriter writer, ActorSubject value)
    { writer.Write((int)value.ActorType); writer.Write(value.Name); writer.Write(value.Verb); writer.Write(value.EntityId); }
    public static ActorSubject ReadSubject(BinaryReader reader, bool archiveManifest = false)
    { var subject = new ActorSubject((ActorType)reader.ReadInt32(), reader.ReadString(), reader.ReadString(), reader.ReadString()); ValidateSubject(subject, archiveManifest); return subject; }
    static void ValidateSubject(ActorSubject value, bool archiveManifest = false)
    {
        if ((!archiveManifest && value.ActorType != ActorType.Realtime) || !Enum.IsDefined(value.ActorType) || string.IsNullOrWhiteSpace(value.Name) || string.IsNullOrWhiteSpace(value.Verb)
            || string.IsNullOrWhiteSpace(value.EntityId) || value.Name.Length > 256 || value.Verb.Length > 256 || value.EntityId.Length > 16384)
            throw new InvalidDataException("Invalid realtime subject.");
    }
    static byte[] ReadExactly(BinaryReader reader, int count)
    { var bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); return bytes; }
}
