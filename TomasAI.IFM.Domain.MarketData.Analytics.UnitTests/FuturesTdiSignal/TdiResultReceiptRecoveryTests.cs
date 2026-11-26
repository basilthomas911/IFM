using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public sealed class TdiResultReceiptRecoveryTests
{
    static string Root() => Path.Combine(Path.GetTempPath(), "IFM-tdi-receipts-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("source")]
    [InlineData("source-ambiguous")]
    [InlineData("database")]
    [InlineData("database-ambiguous")]
    [InlineData("completion")]
    [InlineData("completion-ambiguous")]
    public async Task Pending_original_cross_and_both_event_ids_survive_each_failure_and_restart(string failure)
    {
        var root = Root(); var source = TdiReceiptFixture.Source();
        FuturesTdiResultReceipt original;
        using (var store = new FuturesTdiResultReceipts(root))
        {
            var before = new TdiReceiptFixture.Scenario(store)
            {
                FailSource = failure == "source", ThrowAfterSource = failure == "source-ambiguous",
                FailDatabase = failure == "database", ThrowAfterDatabase = failure == "database-ambiguous",
                FailCompletion = failure == "completion", ThrowAfterCompletion = failure == "completion-ambiguous"
            };
            await before.Start();
            await Assert.ThrowsAsync<IOException>(() => before.Process(source).AsTask());
            original = Assert.Single(store.Pending());
            Assert.Equal(FuturesTdiCrossType.Bullish, original.Generated.FuturesTdiSignal.Cross);
        }
        using (var store = new FuturesTdiResultReceipts(root))
        {
            var after = new TdiReceiptFixture.Scenario(store) { Persisted = original.Generated.FuturesTdiSignal };
            await after.Start();
            Assert.True(await after.State.RecoverAsync(after.Projector)); // No fresh RSI input is required.
            Assert.Empty(store.Pending());
            Assert.True(await after.Process(source));
            var recovered = store.Find(original.Generated.Id)!;
            Assert.Equal(TdiReceiptStage.Completed, recovered.Stage);
            Assert.Equal(MessagePackSerializer.Serialize(original.Generated), MessagePackSerializer.Serialize(recovered.Generated));
            Assert.Equal(MessagePackSerializer.Serialize(original.Completed), MessagePackSerializer.Serialize(recovered.Completed));
            Assert.Equal(MessageOrigins.Get(original.Generated), MessageOrigins.Get(recovered.Generated));
            Assert.Equal(FuturesTdiCrossType.Bullish, after.Persisted!.Cross);
            Assert.All(after.Sources, message => Assert.Equal(original.Generated.Id, message.Id));
            Assert.All(after.Completions, message => Assert.Equal(original.Completed.Id, message.Id));
        }
    }

    [Fact]
    public async Task Completed_receipt_is_loaded_on_cold_restart_and_not_published_or_applied_again()
    {
        var root = Root(); var source = TdiReceiptFixture.Source(); Guid identity;
        using (var store = new FuturesTdiResultReceipts(root))
        {
            var before = new TdiReceiptFixture.Scenario(store); await before.Start(); Assert.True(await before.Process(source));
            identity = Assert.Single(before.Sources).Id;
        }
        using var restartedStore = new FuturesTdiResultReceipts(root);
        var after = new TdiReceiptFixture.Scenario(restartedStore); await after.Start();
        // A new transport publication of the same window also resolves to the original result.
        Assert.True(await after.Process(source with { Id = Guid.NewGuid(), CommandId = Guid.NewGuid() }));
        Assert.Empty(after.Sources); Assert.Empty(after.Completions); Assert.Equal(0, after.DatabaseWrites);
        Assert.Equal(FuturesTdiCrossType.Bullish, restartedStore.Find(identity)!.Generated.FuturesTdiSignal.Cross);
    }

    [Fact]
    public async Task Older_pending_completion_is_recovered_before_a_later_window_is_evaluated()
    {
        using var store = new FuturesTdiResultReceipts(Root());
        var scenario = new TdiReceiptFixture.Scenario(store) { FailCompletion = true }; await scenario.Start();
        await Assert.ThrowsAsync<IOException>(() => scenario.Process(TdiReceiptFixture.Source()).AsTask());
        var first = Assert.Single(store.Pending()); scenario.FailCompletion = false;
        Assert.True(await scenario.Process(TdiReceiptFixture.Source(1)));
        Assert.Equal(2, scenario.Completions.Count);
        Assert.Equal(first.Completed.Id, scenario.Completions[0].Id);
        Assert.Equal(FuturesTdiCrossType.Bullish, scenario.Completions[0].FuturesTdiSignal.Cross);
        Assert.Equal(FuturesTdiCrossType.None, scenario.Completions[1].FuturesTdiSignal.Cross);
    }

    [Fact]
    public async Task Conflicting_replay_is_rejected_before_changing_the_original_result()
    {
        using var store = new FuturesTdiResultReceipts(Root());
        var scenario = new TdiReceiptFixture.Scenario(store); await scenario.Start();
        var source = TdiReceiptFixture.Source(); Assert.True(await scenario.Process(source));
        var changed = source.FuturesRsiSignals.ToArray(); changed[0] = changed[0] with { RSI = changed[0].RSI + 1 };
        await Assert.ThrowsAsync<InvalidDataException>(() => scenario.Process(source with { FuturesRsiSignals = changed }).AsTask());
        Assert.Equal(1, scenario.DatabaseWrites); Assert.Single(scenario.Completions);
        Assert.Equal(FuturesTdiCrossType.Bullish, scenario.Persisted!.Cross);
    }

    [Fact]
    public async Task Corrupt_pending_receipt_blocks_recovery()
    {
        var root = Root();
        using (var store = new FuturesTdiResultReceipts(root))
        {
            var scenario = new TdiReceiptFixture.Scenario(store) { FailSource = true }; await scenario.Start();
            await Assert.ThrowsAsync<IOException>(() => scenario.Process(TdiReceiptFixture.Source()).AsTask());
        }
        var path = Directory.GetFiles(Path.Combine(root, "pending"), "*.tdi").Single();
        var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => new FuturesTdiResultReceipts(root));
    }

    [Fact]
    public void Receipt_root_has_one_process_owner()
    {
        var root = Root(); using var first = new FuturesTdiResultReceipts(root);
        Assert.Throws<IOException>(() => new FuturesTdiResultReceipts(root));
    }

    [Fact]
    public async Task Legacy_applied_window_without_a_receipt_cannot_be_recomputed_with_a_new_identity()
    {
        var source = TdiReceiptFixture.Source();
        using var originalStore = new FuturesTdiResultReceipts();
        var original = new TdiReceiptFixture.Scenario(originalStore); await original.Start(); Assert.True(await original.Process(source));
        using var missingReceipts = new FuturesTdiResultReceipts(Root());
        var restarted = new TdiReceiptFixture.Scenario(missingReceipts) { Persisted = original.Persisted }; await restarted.Start();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => restarted.Process(source).AsTask());
        Assert.Contains("TDI.LEGACY_RECEIPT_GAP", error.Message);
        Assert.Empty(restarted.Sources); Assert.Equal(0, restarted.DatabaseWrites);
    }

    [Fact]
    public async Task Equivalent_database_decimal_scale_resolves_the_original_receipt()
    {
        using var store = new FuturesTdiResultReceipts(Root());
        var scenario = new TdiReceiptFixture.Scenario(store); await scenario.Start();
        var source = TdiReceiptFixture.Source(); Assert.True(await scenario.Process(source));
        var reloaded = source with { FuturesRsiSignals = source.FuturesRsiSignals.Select(signal => signal with
        { Price = decimal.Parse(signal.Price.ToString("F8", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture) }).ToArray() };
        Assert.True(await scenario.Process(reloaded));
        Assert.Single(scenario.Sources); Assert.Single(scenario.Completions); Assert.Equal(1, scenario.DatabaseWrites);
    }

    [Fact]
    public async Task Failed_seed_is_retried_instead_of_marked_complete()
    {
        using var receipts = new FuturesTdiResultReceipts(Root());
        var scenario = new TdiReceiptFixture.Scenario(receipts); await scenario.Start();
        var source = TdiReceiptFixture.Source();
        scenario.Db.GetLastFuturesTdiSignalAsync("ESU6", TdiReceiptFixture.Date, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId)
            .Returns(Task.FromException<TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesTdiSignalReadModel?>(new IOException("Injected seed failure.")));
        await Assert.ThrowsAsync<IOException>(() => scenario.Process(source).AsTask());
        Assert.Empty(receipts.Pending()); Assert.Empty(scenario.Sources);
        scenario.Db.GetLastFuturesTdiSignalAsync("ESU6", TdiReceiptFixture.Date, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId)
            .Returns(TdiReceiptFixture.Prior);
        Assert.True(await scenario.Process(source));
        Assert.Equal(FuturesTdiCrossType.Bullish, scenario.Persisted!.Cross);
        await scenario.Db.Received(2).GetLastFuturesTdiSignalAsync("ESU6", TdiReceiptFixture.Date, TimeFrameType.OneMinute, FuturesTdiConfiguration.StandardConfigurationId);
    }

    [Fact]
    public async Task Failed_receipt_write_prevents_all_external_effects()
    {
        var root = Root(); var source = TdiReceiptFixture.Source();
        var probe = new FuturesTdiSignalRealtimeState();
        Assert.True(probe.TryEvaluate(source, source.FuturesRsiSignals, FuturesTdiConfiguration.Standard, out var proposed));
        using var receipts = new FuturesTdiResultReceipts(root);
        var scenario = new TdiReceiptFixture.Scenario(receipts); await scenario.Start();
        var obstruction = Path.Combine(root, "pending", proposed.Generated.Id.ToString("N") + ".tdi.tmp");
        Directory.CreateDirectory(obstruction);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => scenario.Process(source).AsTask());
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Empty(scenario.Sources); Assert.Empty(scenario.Completions); Assert.Equal(0, scenario.DatabaseWrites);
        Directory.Delete(obstruction); // Explicit, empty directory owned by this test.
        Assert.True(await scenario.Process(source));
        Assert.Equal(FuturesTdiCrossType.Bullish, scenario.Persisted!.Cross);
    }

    [Fact]
    public async Task Actor_startup_resumes_pending_publication_without_a_new_RSI_signal()
    {
        var root = Root(); FuturesTdiResultReceipt original;
        using (var receipts = new FuturesTdiResultReceipts(root))
        {
            var before = new TdiReceiptFixture.Scenario(receipts) { FailSource = true }; await before.Start();
            await Assert.ThrowsAsync<IOException>(() => before.Process(TdiReceiptFixture.Source()).AsTask());
            original = Assert.Single(receipts.Pending());
        }
        using var restored = new FuturesTdiResultReceipts(root);
        var context = Substitute.For<IFuturesTdiSignalRealtimeContext>();
        var after = new TdiReceiptFixture.Scenario(restored, context);
        context.Projector.Returns(after.Projector); context.DbFactory.Returns(after.Factory); context.Receipts.Returns(restored);
        context.Logger.Returns(NullLogger<FuturesTdiSignalRealtimeActor>.Instance);
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorMailbox>());
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        var actor = new FuturesTdiSignalRealtimeActor(context);
        try
        {
            await actor.StartAsync(supervisor);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (restored.Pending().Length != 0 && timeout.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
            Assert.Empty(restored.Pending());
            Assert.Equal(original.Generated.Id, Assert.Single(after.Sources).Id);
            Assert.Equal(original.Completed.Id, Assert.Single(after.Completions).Id);
            Assert.Equal(FuturesTdiCrossType.Bullish, after.Persisted!.Cross);
        }
        finally { await actor.StopAsync(); }
    }

    [Fact]
    public async Task Database_microsecond_precision_resolves_the_original_full_precision_receipt()
    {
        using var store = new FuturesTdiResultReceipts(Root());
        var scenario = new TdiReceiptFixture.Scenario(store); await scenario.Start();
        var source = TdiReceiptFixture.Source();
        source = source with { FuturesRsiSignals = source.FuturesRsiSignals.Select(signal => signal with
        { Timestamp = signal.Timestamp.Add(TimeSpan.FromTicks(7)), SourceEventTimestamp = signal.SourceEventTimestamp.AddTicks(7) }).ToArray() };
        Assert.True(await scenario.Process(source));
        var original = Assert.Single(scenario.Sources);
        var reloaded = source with { FuturesRsiSignals = source.FuturesRsiSignals.Select(signal => signal with
        { Timestamp = new TimeOnly(signal.Timestamp.Ticks / 10 * 10), SourceEventTimestamp = new DateTime(signal.SourceEventTimestamp.Ticks / 10 * 10, DateTimeKind.Utc) }).ToArray() };
        Assert.True(await scenario.Process(reloaded));
        Assert.Single(scenario.Sources); Assert.Single(scenario.Completions);
        Assert.Equal(7, original.FuturesTdiSignal.SourceEventTimestamp.Ticks % 10);
        Assert.Equal(original.Id, store.Find(original.Id)!.Generated.Id);
        Assert.Equal(original.FuturesTdiSignal.SourceEventTimestamp, store.Find(original.Id)!.Generated.FuturesTdiSignal.SourceEventTimestamp);
    }
}
