using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Time;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class RecordedMonitoringAuthorityTests
{
    static readonly string SourceSha = new('a', 64), BoundarySha = new('b', 64);
    static RecordedRealtimeGenerationAcceptance Proof() => new() { Subject = new(ActorType.Realtime,
        "IronCondorTradePositionRealtime", "IronCondorMonitoringInputsChanged", "metadata-fixture"), EventId = Guid.NewGuid(),
        OriginalGenerationId = Guid.NewGuid(), PayloadSha256 = new('c', 64), PositionSequence = 7, RouteGeneration = 3 };
    static RecordedMonitoringAuthority Authority(params RecordedRealtimeGenerationAcceptance[] entries)
        => new(new(1, "OriginalAcceptedMonitoringAuthority", SourceSha, BoundarySha, entries.Length), entries);
    static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    [Fact]
    public void Only_exact_original_permission_can_map_to_a_nonempty_replay_generation()
    {
        var proof = Proof(); var authority = Authority(proof); var current = Guid.NewGuid();
        Assert.True(authority.Allows(proof, current)); Assert.False(authority.Allows(proof, Guid.Empty));
        Assert.False(authority.Allows(proof with { EventId = Guid.NewGuid() }, current));
        Assert.False(authority.Allows(proof with { OriginalGenerationId = Guid.NewGuid() }, current));
        Assert.False(authority.Allows(proof with { RouteGeneration = 4 }, current));
        Assert.False(authority.Allows(proof with { PositionSequence = 8 }, current));
        Assert.False(authority.Allows(proof with { PayloadSha256 = new('d', 64) }, current));
        Assert.False(authority.Allows(proof with { Subject = proof.Subject with { EntityId = "another-position" } }, current));
    }
    [Fact]
    public void Clock_has_no_generation_override_until_one_immutable_authority_is_installed()
    {
        var clock = new HistoricalReplayClock(DateTimeOffset.UtcNow); var proof = Proof(); var authority = Authority(proof);
        Assert.False(clock.Allows(proof, Guid.NewGuid())); clock.SetGenerationAuthority(authority);
        Assert.True(clock.Allows(proof, Guid.NewGuid())); clock.SetGenerationAuthority(authority);
        Assert.Throws<InvalidOperationException>(() => clock.SetGenerationAuthority(Authority()));
    }
    [Fact]
    public void Pinned_stream_roundtrips_and_rejects_tamper_duplicate_and_wrong_source_boundary()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm_monitoring_metadata_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "accepted.authority"); var proof = Proof(); Authority(proof).WriteNew(path);
            var sha = Hash(path); Assert.True(RecordedMonitoringAuthority.Read(path, sha).Allows(proof, Guid.NewGuid()));
            using var config = JsonDocument.Parse(JsonSerializer.Serialize(new { recordedMonitoringAuthority = new { path, sha256 = sha,
                sourceArchiveCopyManifestSha256 = SourceSha, initialArchiveBoundarySha256 = new string('e', 64) } }));
            Assert.Throws<InvalidDataException>(() => RecordedMonitoringAuthority.LoadFromConfiguration(config.RootElement, root));
            Assert.Throws<InvalidDataException>(() => Authority(proof, proof));
            File.AppendAllText(path, "changed"); Assert.Throws<InvalidDataException>(() => RecordedMonitoringAuthority.Read(path, sha));
        }
        finally { foreach (var path in Directory.GetFiles(root)) File.Delete(path); Directory.Delete(root); }
    }
    [Fact]
    public async Task Actual_archive_join_requires_matching_accepted_receipt_and_leaves_stale_timer_unapproved()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm_monitoring_archive_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await using var archive = new DurableRealtimeSessionArchive(new() { Enabled = true, Directory = root, SessionId = "metadata-test" });
        var baselinePrefix = await archive.GetCommittedPrefixAsync();
        string HashInitial(RealtimeSessionArchivePrefixFile value)
        {
            using var input = new FileStream(Path.Combine(baselinePrefix.SessionDirectory, value.RelativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bytes = new byte[checked((int)value.Length)]; input.ReadExactly(bytes);
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        var initialFiles = baselinePrefix.Files.Select(value => new RealtimeSessionArchiveBoundaryFile(value.RelativePath, value.Length, HashInitial(value))).ToArray();
        var accepted = Proof(); var stale = Proof();
        async Task<RecordedRealtimeGenerationAcceptance> RecordSource(RecordedRealtimeGenerationAcceptance value)
        {
            var position = new TomasAI.IFM.Domain.Trade.Shared.StrategyPositionId(new(1, 2, 3, 4), Guid.NewGuid());
            value = value with { Subject = value.Subject with { EntityId = position.Format() } };
            var input = new IronCondorMonitoringInputsChangedEvent { Subject = value.Subject, Id = value.EventId, EntityId = position,
                MonitoringGenerationId = value.OriginalGenerationId, ReceivedOn = DateTime.UtcNow, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), PositionSnapshot = new() { Id = position, PositionSequence = value.PositionSequence, RouteGeneration = value.RouteGeneration } };
            var wire = MessagePackSerializer.Serialize(input, MessagePackBinarySerializer.Options);
            await archive.RecordAsync(new() { Kind = "RealtimePublication", LogicalSourceKey = "original/" + input.Id,
                Subject = input.Subject.ToString(), EventId = input.Id, OriginalWire = wire });
            return value with { PayloadSha256 = Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(input))) };
        }
        accepted = await RecordSource(accepted); _ = await RecordSource(stale);
        await archive.RecordAsync(new() { Kind = "MonitoringGenerationAccepted", LogicalSourceKey = "permission/" + accepted.EventId,
            Subject = accepted.Subject.ToString(), EventId = accepted.EventId, GenerationId = accepted.OriginalGenerationId,
            OriginalWire = MessagePackSerializer.Serialize(accepted) });
        var derived = ReplayMonitoringAuthority.Derive(archive);
        Assert.Single(derived); Assert.True(RecordedMonitoringAuthority.Same(accepted, derived[accepted.EventId])); Assert.False(derived.ContainsKey(stale.EventId));
        var analysis = Path.Combine(root, "analysis-copy");
        var epoch = Path.GetFileName(archive.SessionDirectory); var epochCopy = Path.Combine(analysis, epoch); Directory.CreateDirectory(epochCopy);
        foreach (var item in (await archive.GetCommittedPrefixAsync()).Files)
        {
            var copy = Path.Combine(epochCopy, item.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            using var input = new FileStream(Path.Combine(archive.SessionDirectory, item.RelativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = File.Create(copy); var bytes = new byte[checked((int)item.Length)]; input.ReadExactly(bytes); output.Write(bytes);
        }
        var boundary = new RealtimeSessionArchiveBoundary(1, "FrozenOwnerSourceArchiveFloor", "metadata-test", baselinePrefix.Protocol,
            "component-boundary", "component-held-lease", "source-outbox", new('d', 64), new('e', 64), 0, initialFiles);
        var boundaryPath = Path.Combine(root, "component-boundary.json"); await File.WriteAllTextAsync(boundaryPath, JsonSerializer.Serialize(boundary));
        var authorityPath = Path.Combine(root, "exported.authority");
        await ReplayMonitoringAuthority.ExportAsync(analysis, "metadata-test", SourceSha, boundaryPath, Hash(boundaryPath), authorityPath);
        var exported = RecordedMonitoringAuthority.Read(authorityPath, Hash(authorityPath)); Assert.Single(exported.Entries);
        Assert.True(exported.Allows(accepted, Guid.NewGuid())); Assert.False(exported.Allows(stale, Guid.NewGuid()));
        var orphan = Proof(); await archive.RecordAsync(new() { Kind = "MonitoringGenerationAccepted", LogicalSourceKey = "orphan/" + orphan.EventId,
            Subject = orphan.Subject.ToString(), EventId = orphan.EventId, GenerationId = orphan.OriginalGenerationId,
            OriginalWire = MessagePackSerializer.Serialize(orphan) });
        Assert.Throws<InvalidDataException>(() => ReplayMonitoringAuthority.Derive(archive));
        // Archive files intentionally retained as finite metadata component evidence, never a full session.
    }
}
