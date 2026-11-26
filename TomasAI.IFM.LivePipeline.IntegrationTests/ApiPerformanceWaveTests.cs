using System.Buffers;
using System.Text.Json;
using MessagePack;
using Npgsql;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.IntegrationTesting;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public class ApiPerformanceWaveTests
{
    static string Root(string name) => Path.Combine(Path.GetTempPath(), "ifm-wave-" + name + "-" + Guid.NewGuid().ToString("N"));
    static void Cleanup(string root)
    {
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "ifm-wave-"), root, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, true);
    }
    [Fact]
    public async Task Wal_commits_ordered_groups_before_completion_and_restores_after_restart()
    {
        var root = Root("wal");
        var payloads = Enumerable.Range(0, 512).Select(n => BitConverter.GetBytes(n)).ToArray();
        await using (var wal = new FuturesItiInputWal(root))
        {
            var positions = await Task.WhenAll(payloads.Select(bytes => wal.AppendAsync(bytes, default).AsTask()));
            Assert.Equal(positions.OrderBy(position => position.Segment).ThenBy(position => position.EndOffset), positions);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wal.AppendAsync([1], cancelled.Token).AsTask());
        }
        await using (var wal = new FuturesItiInputWal(root))
        {
            var count = 0;
            await foreach (var record in wal.ReplayAsync(default)) Assert.Equal(payloads[count++], record.Payload);
            Assert.Equal(payloads.Length, count);
        }
        Cleanup(root);
    }
    [Fact]
    public async Task Pooled_wal_completions_cannot_be_consumed_twice_and_corrupt_frames_block_replay()
    {
        var root = Root("wal-integrity");
        await using (var wal = new FuturesItiInputWal(root))
        {
            var operation = wal.AppendAsync([1, 2, 3], default);
            _ = await operation;
            Assert.Throws<InvalidOperationException>(() => operation.GetAwaiter().GetResult());
            _ = await wal.AppendAsync([4, 5, 6], default);
        }
        var file = Directory.GetFiles(root, "*.wal").Single();
        var bytes = File.ReadAllBytes(file); bytes[^1] ^= 1; File.WriteAllBytes(file, bytes);
        await using (var wal = new FuturesItiInputWal(root))
            await Assert.ThrowsAsync<InvalidDataException>(async () => { await foreach (var record in wal.ReplayAsync(default)) { _ = record; } });
        Assert.True(File.Exists(file)); Cleanup(root);
    }
    [Fact]
    public async Task Binary_eod_spool_retains_retry_ids_timestamps_legacy_records_and_rejects_damage()
    {
        var root = Root("eod"); Directory.CreateDirectory(root);
        var source = new FuturesEodDataInsertedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), ReceivedOn = new(2026, 11, 23, 21, 0, 0, DateTimeKind.Utc) };
        var row = new BufferedFuturesEodRow(new FuturesEodDataV2ReadModel { ContractId = "ES-test", Symbol = "ES", ValueDate = new(2026, 11, 23), ClosePrice = 6000.25m }, true) { HistorySequenceId = 123 };
        var legacy = EodHistoryEntry.Create(source, row, null, 42);
        File.WriteAllText(Path.Combine(root, "0000000000000000001-legacy.json"), JsonSerializer.Serialize(legacy));
        string binary;
        using (var spool = new EodHistorySpool(root))
        {
            var entry = EodHistoryEntry.Create(source, row, null, 43);
            await spool.AppendAsync(entry); binary = entry.FileName;
            entry.Row!.HistorySequenceId = 456; await EodHistorySpool.SaveAsync(entry);
            var batch = await spool.ReadBatchAsync(8); Assert.Equal(2, batch.Count);
            Assert.Equal(123, batch[0].Row!.HistorySequenceId); Assert.Equal(456, batch[1].Row!.HistorySequenceId);
            Assert.Equal(source.Id, batch[1].Event.Id); Assert.Equal(source.CommandId, batch[1].Event.CommandId);
            Assert.Equal(source.ReceivedOn, batch[1].Event.ReceivedOn); Assert.Equal(6000.25m, batch[1].Row!.Snapshot.ClosePrice);
            spool.Acknowledge(batch[0]);
        }
        using (var spool = new EodHistorySpool(root))
        {
            Assert.Equal(1, spool.Pending); var batch = await spool.ReadBatchAsync(8); Assert.Equal(456, batch.Single().Row!.HistorySequenceId);
            var bytes = File.ReadAllBytes(binary); bytes[^1] ^= 1; File.WriteAllBytes(binary, bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => spool.ReadBatchAsync(8)); Assert.True(File.Exists(binary));
        }
        Cleanup(root);
    }
    [Fact]
    public async Task Pooled_postgres_decode_owns_its_arrays_after_the_input_pool_is_reused()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("pooledwave"); await infrastructure.StartAsync();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
            { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString); await connection.OpenAsync();
        var bytes = new byte[128_000]; Random.Shared.NextBytes(bytes);
        var payload = MessagePackBinarySerializer.Shared.Serialize(bytes)!;
        await using var command = new NpgsqlCommand("SELECT $1::bytea", connection); command.Parameters.AddWithValue(payload);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        var decoded = PooledPostgresPayload.Deserialize<byte[]>(reader, 0)!;
        var decodedMemory = PooledPostgresPayload.Deserialize<ReadOnlyMemory<byte>>(reader, 0);
        var reused = ArrayPool<byte>.Shared.Rent(payload.Length); reused.AsSpan().Fill(0xCC); ArrayPool<byte>.Shared.Return(reused);
        Assert.Equal(bytes, decoded); Assert.NotSame(bytes, decoded);
        Assert.Equal(bytes, decodedMemory.ToArray());
    }
    [Fact]
    public async Task Eod_retry_groups_remain_durable_and_large_backlogs_drain_beyond_the_bounded_index()
    {
        var root = Root("eod-backlog");
        using (var spool = new EodHistorySpool(root))
        {
            for (var n = 0; n < 16; n++)
                await spool.AppendAsync(EodHistoryEntry.Create(new FuturesEodDataInsertedEvent { Id = Guid.NewGuid(), ReceivedOn = DateTime.UnixEpoch },
                    new BufferedFuturesEodRow(new FuturesEodDataV2ReadModel { ContractId = "ES-test", Symbol = "ES" }, true), null, n));
            var batch = await spool.ReadBatchAsync(16);
            for (var n = 0; n < batch.Count; n++) batch[n].Row!.HistorySequenceId = n + 100;
            await EodHistorySpool.SaveBatchAsync(batch);
        }
        using (var spool = new EodHistorySpool(root))
        {
            var batch = await spool.ReadBatchAsync(16);
            Assert.Equal(Enumerable.Range(100, 16).Select(n => (long?)n), batch.Select(entry => entry.Row!.HistorySequenceId));
            await spool.AppendAsync(EodHistoryEntry.Create(new FuturesEodDataInsertedEvent { Id = Guid.NewGuid(), ReceivedOn = DateTime.UnixEpoch }, null, null, 0));
        }
        var template = Directory.GetFiles(root, "*.bin")[0];
        for (var n = 0; n < 4200; n++) File.Copy(template, Path.Combine(root, $"{n:D19}-backlog.bin"));
        using (var spool = new EodHistorySpool(root))
        {
            var total = spool.Pending; Assert.Equal(4217, total); var read = 0;
            Guid? newest = null; Guid last = default;
            while (spool.Pending > 0)
            {
                var batch = await spool.ReadBatchAsync(512); Assert.NotEmpty(batch);
                foreach (var entry in batch) { last = entry.Event.Id; spool.Acknowledge(entry); read++; }
                if (newest is null)
                {
                    newest = Guid.NewGuid();
                    await spool.AppendAsync(EodHistoryEntry.Create(new FuturesEodDataInsertedEvent { Id = newest.Value, ReceivedOn = DateTime.UnixEpoch }, null, null, 0));
                    total++;
                }
            }
            Assert.Equal(total, read); Assert.Equal(newest, last);
        }
        Cleanup(root);
    }
    [Fact]
    public async Task Completed_segment_marker_reclaims_sources_without_replaying_a_retired_group()
    {
        var root = Root("eod-completed");
        using (var spool = new EodHistorySpool(root))
        {
            await spool.AppendAsync(EodHistoryEntry.Create(new FuturesEodDataInsertedEvent
                { Id = Guid.NewGuid(), ReceivedOn = DateTime.UnixEpoch }, null, null, 0));
            var original = Directory.GetFiles(root, "*.bin").Single();
            var bytes = File.ReadAllBytes(original);
            await EodHistorySpool.SaveBatchAsync(await spool.ReadBatchAsync(1));
            var segment = Directory.GetFiles(root, "*.seg").Single();
            // Simulate a completed durable rename whose final unlink did not survive.
            File.Move(segment, Path.ChangeExtension(segment, ".done"));
            File.WriteAllBytes(original, bytes);
        }
        using (var spool = new EodHistorySpool(root))
        {
            Assert.Equal(0, spool.Pending); Assert.Empty(await spool.ReadBatchAsync(1));
            Assert.Empty(Directory.GetFiles(root, "*.done")); Assert.Empty(Directory.GetFiles(root, "*.bin"));
        }
        Cleanup(root);
    }
    [Fact]
    public async Task Segment_handoff_and_partial_ack_restart_keep_original_retry_ids_without_double_counting_sources()
    {
        var root = Root("eod-handoff");
        Dictionary<string, byte[]> originals;
        Guid[] identities;
        using (var spool = new EodHistorySpool(root))
        {
            for (var n = 0; n < 3; n++) await spool.AppendAsync(EodHistoryEntry.Create(
                new FuturesEodDataInsertedEvent { Id = Guid.NewGuid(), ReceivedOn = DateTime.UnixEpoch },
                new BufferedFuturesEodRow(new FuturesEodDataV2ReadModel { ContractId = "ES", Symbol = "ES" }, true), null, n));
            originals = Directory.GetFiles(root, "*.bin").ToDictionary(path => path, File.ReadAllBytes);
            var batch = await spool.ReadBatchAsync(3); identities = batch.Select(entry => entry.Event.Id).ToArray();
            for (var n = 0; n < batch.Count; n++) batch[n].Row!.HistorySequenceId = n + 100;
            await EodHistorySpool.SaveBatchAsync(batch);
            // Simulate termination after the segment commit, before retirement of originals.
            foreach (var original in originals) File.WriteAllBytes(original.Key, original.Value);
        }
        using (var spool = new EodHistorySpool(root))
        {
            Assert.Equal(3, spool.Pending); Assert.Empty(Directory.GetFiles(root, "*.bin"));
            var first = (await spool.ReadBatchAsync(1)).Single(); Assert.Equal(100, first.Row!.HistorySequenceId);
            spool.Acknowledge(first); Assert.Equal(2, spool.Pending); Assert.Single(Directory.GetFiles(root, "*.seg"));
        }
        using (var spool = new EodHistorySpool(root))
        {
            // A partially acknowledged segment replays, using the same durable row IDs.
            var batch = await spool.ReadBatchAsync(3);
            Assert.Equal(identities, batch.Select(entry => entry.Event.Id));
            Assert.Equal(new long?[] { 100, 101, 102 }, batch.Select(entry => entry.Row!.HistorySequenceId));
            Assert.All(batch, entry => Assert.Equal(DateTime.UnixEpoch, entry.Event.ReceivedOn));
            foreach (var entry in batch) spool.Acknowledge(entry);
            Assert.Equal(0, spool.Pending); Assert.Empty(Directory.GetFiles(root, "*.seg"));
        }
        Cleanup(root);
    }
}
