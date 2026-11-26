using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class EodBufferedRecoveryTests
{
    [Fact]
    public void Projection_factory_preserves_source_origin_before_background_capture()
    {
        var source = BufferedEodFixture.Source(1);
        var origin = new MessageOrigin(DateTimeOffset.UtcNow, "eod-origin", causationId: source.Id);
        MessageOrigins.Restore(source, origin);
        var generated = TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.VxFuturesEodDataEventFactory.Create(source,
            new() { ContractId = "VX-ORIGIN", ValueDate = source.EntityId.ValueDate, Price = 20, Size = 1 });
        Assert.Equal(origin, MessageOrigins.Get(generated));
        Assert.Equal(generated.Id, TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.VxFuturesEodDataEventFactory.Create(source,
            generated.VixFuturesTickData).Id);
    }
    [Fact]
    public async Task Capture_owner_is_exclusive_and_corrupt_progress_fails_closed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "IFM-eod-corruption-" + Guid.NewGuid().ToString("N"));
        var source = BufferedEodFixture.Source(1);
        using (var journal = new EodCaptureJournal(directory))
        {
            Assert.Throws<IOException>(() => new EodCaptureJournal(directory));
            var entry = EodHistoryEntry.Create(source, new(source.FuturesEodData, true), null, 0);
            entry.CompletionBytes = MessagePack.MessagePackSerializer.Serialize(new FuturesEodDataInsertedCompleteEvent { Id = source.Id, CommandId = source.CommandId });
            await journal.PrepareAsync(entry);
            entry.Row!.HistorySequenceId = 123;
            await journal.AdvanceAsync(new[] { entry }, 1);
            var count = Directory.GetFiles(Path.Combine(directory, "recovery-v1", "pending"), "*.eodg").Length;
            await journal.AdvanceAsync(new[] { entry }, 1); // Identical retry requires no additional progress commit.
            Assert.Equal(count, Directory.GetFiles(Path.Combine(directory, "recovery-v1", "pending"), "*.eodg").Length);
        }
        using (var recovered = new EodCaptureJournal(directory))
        {
            var entry = recovered.Batch(1).Single(); Assert.Equal(123, entry.Row!.HistorySequenceId); Assert.Equal(1, entry.Stage);
        }
        var path = Directory.GetFiles(Path.Combine(directory, "recovery-v1", "pending"), "*.eodg").Single();
        var corrupt = File.ReadAllBytes(path); corrupt[^1] ^= 1; File.WriteAllBytes(path, corrupt);
        Assert.Throws<InvalidDataException>(() => new EodCaptureJournal(directory));
    }
    [Theory]
    [InlineData("source")] [InlineData("database")] [InlineData("completion")]
    public async Task Frozen_receipts_recover_in_order_and_preserve_original_publication_and_history_ids(string failure)
    {
        var directory = Path.Combine(Path.GetTempPath(), "IFM-eod-recovery-" + Guid.NewGuid().ToString("N"));
        var a = BufferedEodFixture.Source(1, Guid.NewGuid().ToString("N"));
        var b = BufferedEodFixture.Source(2, a.EntityId.ContractId);
        var origin = new MessageOrigin(DateTimeOffset.UtcNow.AddMinutes(-1), "eod-test", Guid.NewGuid(), Guid.NewGuid(), a.Id);
        MessageOrigins.Restore(a, origin);
        var sourceIds = new List<Guid>(); var completionIds = new List<Guid>();
        await using (var first = new BufferedEodFixture(directory))
        {
            first.Send = input =>
            {
                if (input is FuturesEodDataInsertedEvent) { sourceIds.Add(input.Id); if (failure == "source") throw new IOException("source delivery lost"); }
                if (input is FuturesEodDataInsertedCompleteEvent) { completionIds.Add(input.Id); if (failure == "completion") throw new IOException("completion delivery lost"); }
                return ValueTask.CompletedTask;
            };
            first.Persist = _ => failure == "database" ? Task.FromException(new IOException("committed response lost")) : Task.CompletedTask;
            await first.Start();
            Assert.True(await first.Projector.ProcessRealtimeEventAsync(a));
            Assert.True(await first.Projector.ProcessRealtimeEventAsync(a)); // A captured retry never increments volume/history.
            Assert.True(await first.Projector.ProcessRealtimeEventAsync(b));
            await first.Projector.StopAsync();
            Assert.Equal(failure == "source" ? new[] { a.Id } : new[] { a.Id, b.Id }, sourceIds.Distinct());
        }
        await using (var recovered = new BufferedEodFixture(directory))
        {
            await recovered.Start(); await recovered.Projector.StopAsync();
            var sources = recovered.Sent.OfType<FuturesEodDataInsertedEvent>().ToArray();
            if (failure == "source")
            {
                Assert.Equal(new[] { a.Id, b.Id }, sources.Select(e => e.Id));
                Assert.Equal(origin, MessageOrigins.Get(sources[0]));
            }
            else Assert.Empty(sources);
            var completions = recovered.Sent.OfType<FuturesEodDataInsertedCompleteEvent>().ToArray();
            Assert.Equal(2, completions.Length);
            if (failure == "completion") Assert.Equal(completionIds[0], completions[0].Id);
            if (failure != "completion") Assert.Equal(new long[] { 42, 43 }, recovered.HistoryIds);
            if (failure == "completion") Assert.Empty(recovered.HistoryIds); // Applied stage survives restart.
        }
        await using (var again = new BufferedEodFixture(directory))
        {
            await again.Start();
            Assert.True(await again.Projector.ProcessRealtimeEventAsync(a));
            await again.Projector.StopAsync();
            Assert.Empty(again.Sent); Assert.Empty(again.HistoryIds);
        }
    }
    [Fact]
    public async Task Oversized_capture_does_not_advance_the_live_cache()
    {
        await using var fixture = new BufferedEodFixture(); await fixture.Start();
        var a = BufferedEodFixture.Source(1, Guid.NewGuid().ToString("N"));
        var huge = a with { FuturesEodData = a.FuturesEodData with { Symbol = new string('x', 100_000) } };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Projector.ProcessRealtimeEventAsync(huge).AsTask());
        Assert.False(CurrentFuturesEodCache.Shared.TryGet(a.EntityId.ContractId, a.EntityId.ValueDate, out _));
        Assert.Empty(fixture.Sent);
        Assert.True(await fixture.Projector.ProcessRealtimeEventAsync(a));
        await fixture.Projector.StopAsync(); Assert.Single(fixture.HistoryIds);
    }
    [Fact]
    public async Task Vx_duplicate_capture_preserves_the_original_volume_across_restart()
    {
        var contract = "VX-" + Guid.NewGuid().ToString("N");
        var source = new VixFuturesEodDataInsertedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = new(contract, new(2026, 8, 25)),
            Subject = new(ActorType.Realtime, "FuturesEodData", VixFuturesEodDataInsertedEvent.Verb, contract),
            VixFuturesTickData = new() { ContractId = contract, ValueDate = new(2026, 8, 25), Price = 20, Size = 5 }
        };
        string directory;
        await using (var first = new BufferedEodFixture())
        {
            directory = first.Directory; await first.Start();
            await first.Projector.ProcessRealtimeEventAsync(source); await first.Projector.ProcessRealtimeEventAsync(source);
            await first.Projector.StopAsync();
        }
        await using var recovered = new BufferedEodFixture(directory); await recovered.Start();
        await recovered.Projector.ProcessRealtimeEventAsync(source); await recovered.Projector.StopAsync();
        Assert.Empty(recovered.Sent);
        Assert.True(CurrentVixEodCache.Shared.TryGet(contract, source.EntityId.ValueDate, out var row)); Assert.Equal(5, row.Volume);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Legacy_history_is_migrated_before_fresh_capture(bool originalEightFieldSchema)
    {
        await using var fixture = new BufferedEodFixture(); var a = BufferedEodFixture.Source(1);
        using (var legacy = new EodHistorySpool(fixture.Directory))
        {
            var row = new BufferedFuturesEodRow(a.FuturesEodData, true) { HistorySequenceId = 99 };
            if (originalEightFieldSchema)
                await EodHistorySpool.SaveBinaryAsync(Path.Combine(fixture.Directory, $"{DateTime.UtcNow.Ticks:D19}-{a.Id:N}.bin"),
                    new object?[] { 1, 0, MessagePack.MessagePackSerializer.Serialize(a), row, null, 0L, Guid.Empty, DateTime.UtcNow }, "IFMEOD02");
            else await legacy.AppendAsync(EodHistoryEntry.Create(a, row, null, 0));
        }
        await fixture.Start(); await fixture.Projector.StopAsync();
        Assert.Equal(new long[] { 99 }, fixture.HistoryIds);
        Assert.Equal(a.Id, fixture.Sent.OfType<FuturesEodDataInsertedEvent>().Single().Id);
        await using var reopened = new BufferedEodFixture(fixture.Directory); await reopened.Start(); await reopened.Projector.StopAsync(); Assert.Empty(reopened.Sent);
    }
}
