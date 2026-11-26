using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Time;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

/// <summary>Offline join of original timer inputs and actual pre-calculation generation-permission receipts.</summary>
public static class ReplayMonitoringAuthority
{
    public static async Task ExportAsync(string directory, string sessionId, string sourceManifestSha256, string boundaryPath, string boundarySha256, string output)
    {
        using (var boundaryFile = File.OpenRead(boundaryPath))
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(boundaryFile)), boundarySha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Initial authority archive boundary changed.");
        if (new FileInfo(boundaryPath).Length > 4 * 1024 * 1024) throw new InvalidDataException("Initial boundary exceeds metadata bound.");
        var boundary = JsonSerializer.Deserialize<RealtimeSessionArchiveBoundary>(File.ReadAllBytes(boundaryPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing original archive boundary.");
        if (boundary.ArchiveSessionId != sessionId) throw new InvalidDataException("Authority archive session differs from its initial boundary.");
        var epoch = "archive_" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sessionId)))[..24];
        var floor = RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(Path.Combine(Path.GetFullPath(directory), epoch), boundary);
        await using var archive = new DurableRealtimeSessionArchive(new() { Enabled = true, Directory = Path.GetFullPath(directory),
            SessionId = sessionId, MaximumArchiveBytes = 64L * 1024 * 1024 * 1024 });
        var entries = Derive(archive, floor);
        var authority = new RecordedMonitoringAuthority(new(1, "OriginalAcceptedMonitoringAuthority", sourceManifestSha256, boundarySha256, entries.Count), entries.Values);
        authority.WriteNew(Path.GetFullPath(output));
    }

    public static void ValidateAgainstArchive(DurableRealtimeSessionArchive archive, JsonElement host, string restoreRoot,
        string sourceManifestSha256, string boundarySha256, long initialOrdinal)
    {
        var authority = RecordedMonitoringAuthority.LoadFromConfiguration(host, restoreRoot)
            ?? throw new InvalidDataException("Original replay requires a pinned monitoring-authority file, including an empty one when no inputs were accepted.");
        if (authority.Evidence.SourceArchiveCopyManifestSha256 != sourceManifestSha256 || authority.Evidence.InitialArchiveBoundarySha256 != boundarySha256)
            throw new InvalidDataException("Monitoring authority belongs to a different original archive/boundary.");
        var expected = Derive(archive, initialOrdinal);
        if (expected.Count != authority.Entries.Count || expected.Any(pair => !authority.Entries.TryGetValue(pair.Key, out var actual)
            || !RecordedMonitoringAuthority.Same(pair.Value, actual)))
            throw new InvalidDataException("Monitoring authority differs from actual original input and generation-permission receipts.");
    }

    public static Dictionary<Guid, RecordedRealtimeGenerationAcceptance> Derive(DurableRealtimeSessionArchive archive, long initialOrdinal = 0)
    {
        var sources = new Dictionary<Guid, RecordedRealtimeGenerationAcceptance>();
        var permissions = new Dictionary<Guid, RecordedRealtimeGenerationAcceptance>();
        foreach (var item in archive.ReadCommitted())
        {
            if (item.Ordinal <= initialOrdinal) continue;
            var record = item.Record;
            if (record.Kind == "MonitoringGenerationAccepted")
            {
                var proof = MessagePackSerializer.Deserialize<RecordedRealtimeGenerationAcceptance>(record.OriginalWire);
                RecordedMonitoringAuthority.Validate(proof);
                if (record.Subject != proof.Subject.ToString() || record.EventId != proof.EventId || record.GenerationId != proof.OriginalGenerationId)
                    throw new InvalidDataException("Original generation receipt envelope differs from its payload.");
                Add(permissions, proof);
            }
            else if (record.Kind == "RealtimePublication" && record.Subject.StartsWith("Realtime.IronCondorTradePositionRealtime.IronCondorMonitoringInputsChanged.", StringComparison.Ordinal))
            {
                var changed = MessagePackSerializer.Deserialize<IronCondorMonitoringInputsChangedEvent>(record.OriginalWire, MessagePackBinarySerializer.Options);
                if (record.Subject != changed.Subject.ToString() || record.EventId != changed.Id || changed.EntityId != changed.PositionSnapshot.Id
                    || !changed.EntityId.IsValid || changed.Subject.EntityId != changed.EntityId.Format()
                    || changed.ValueDate == default || changed.ReceivedOn.Kind != DateTimeKind.Utc)
                    throw new InvalidDataException("Original monitoring source envelope differs from its payload.");
                // Domain calculation deliberately fingerprints the canonical uncompressed typed payload;
                // archive framing separately verifies the exact original compressed transport bytes.
                var proof = new RecordedRealtimeGenerationAcceptance { Subject = changed.Subject, EventId = changed.Id,
                    OriginalGenerationId = changed.MonitoringGenerationId, PositionSequence = changed.PositionSnapshot.PositionSequence,
                    RouteGeneration = changed.PositionSnapshot.RouteGeneration,
                    PayloadSha256 = Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(changed))) };
                RecordedMonitoringAuthority.Validate(proof); Add(sources, proof);
            }
        }
        foreach (var proof in permissions.Values)
            if (!sources.TryGetValue(proof.EventId, out var original) || !RecordedMonitoringAuthority.Same(proof, original))
                throw new InvalidDataException("Generation permission has no matching exact original timer input.");
        // A published stale timer has no accepted-generation receipt, so it receives no override.
        return permissions;
    }
    static void Add(Dictionary<Guid, RecordedRealtimeGenerationAcceptance> entries, RecordedRealtimeGenerationAcceptance value)
    {
        if (entries.TryGetValue(value.EventId, out var previous))
        {
            if (!RecordedMonitoringAuthority.Same(previous, value)) throw new InvalidDataException("Conflicting original monitoring identities.");
            return;
        }
        if (entries.Count >= RecordedMonitoringAuthority.MaximumEntries) throw new InvalidDataException("Original monitoring evidence exceeds its record bound.");
        entries.Add(value.EventId, value);
    }
}
