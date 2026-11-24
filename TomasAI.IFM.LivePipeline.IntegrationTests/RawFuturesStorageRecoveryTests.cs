using Cassandra;
using MessagePack;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Testing;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class RawFuturesStorageRecoveryTests
{
    [Fact]
    public async Task Source_recovery_failure_blocks_consumer_readiness_without_deleting_the_journal()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("rawhealth");
        await infrastructure.StartAsync();
        var root = Path.Combine(Path.GetTempPath(), "ifm-raw-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var options = new RawFuturesStorageOptions { Enabled = true, Directory = root, Scope = infrastructure.RunId, NatsUrl = infrastructure.NatsUrl };
        await using var connections = new NatsConnectionManager();
        var transport = new RawFuturesStorageTransport(options, connections);
        using var publisher = new RawFuturesStoragePublisher(options, transport, NullLogger<RawFuturesStoragePublisher>.Instance);
        using var consumer = new RawFuturesStorageConsumer(options, transport, Substitute.For<IDbContextFactory>(), NullLogger<RawFuturesStorageConsumer>.Instance);
        await publisher.StartAsync(CancellationToken.None);
        await consumer.StartAsync(CancellationToken.None);
        await WaitAsync(async () => (await consumer.CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Healthy, TimeSpan.FromSeconds(30));
        var path = RawFuturesJournalFixture.Write(root, RawFuturesJournalFixture.Batch(2, true));
        File.WriteAllBytes(path + ".cursor", new byte[64]);
        await WaitAsync(() => publisher.Failure is not null, TimeSpan.FromSeconds(10));
        var health = await consumer.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Contains("source recovery", health.Description);
        Assert.True(File.Exists(path));
        await consumer.StopAsync(CancellationToken.None);
        await publisher.StopAsync(CancellationToken.None);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Journal_replay_broker_redelivery_and_row_batches_preserve_all_trades_and_quotes()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("rawfutures");
        await infrastructure.StartAsync();
        var root = Path.Combine(Path.GetTempPath(), "ifm-raw-recovery-" + Guid.NewGuid().ToString("N"));
        var original = RawFuturesJournalFixture.Batch(10_000, mixed: true);
        var path = RawFuturesJournalFixture.Write(root, original);
        var settings = new DbConnectionSettings().Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection,
            infrastructure.CqlConnectionString, "System.Data.ScyllaDb");
        await new TradeTickQuoteSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var db = new TradeTickQuoteDbContext(settings, NullLogger<DbProvider>.Instance);
        await using var connections = new NatsConnectionManager();
        var options = new RawFuturesStorageOptions { Enabled = true, Directory = root, Scope = infrastructure.RunId, NatsUrl = infrastructure.NatsUrl, RequireSourceHealth = false };
        var transport = new RawFuturesStorageTransport(options, connections);
        var js = await transport.GetAsync(CancellationToken.None);
        // Commit one message but lose its publish acknowledgement/cursor; restart must resend safely.
        var first = (await RawFuturesJournal.ReadAsync(path, 64, CancellationToken.None)).Batch!;
        await transport.PublishAsync(MessagePackSerializer.Serialize(first), $"{first.SourceEpoch:N}:1:64", CancellationToken.None);
        using (var publisher = new RawFuturesStoragePublisher(options, transport, NullLogger<RawFuturesStoragePublisher>.Instance))
        {
            await publisher.StartAsync(CancellationToken.None);
            await WaitAsync(() => !File.Exists(path), TimeSpan.FromSeconds(45));
            await publisher.StopAsync(CancellationToken.None);
        }
        Assert.False(File.Exists(path)); // durable broker now owns the complete source history
        await connections.DisposeAsync();
        await infrastructure.RestartNatsAsync();
        options = options with { NatsUrl = infrastructure.NatsUrl };
        await using var replayConnections = new NatsConnectionManager();
        transport = new RawFuturesStorageTransport(options, replayConnections);
        js = await transport.GetAsync(CancellationToken.None);
        var factory = Substitute.For<IDbContextFactory>();
        var forwarding = Substitute.For<ITradeTickQuoteDbContext>(); forwarding.DbWriter.Returns(forwarding);
        factory.TradeTickQuoteDb.Returns(forwarding);
        var fail = true;
        var faultCount = 0;
        forwarding.InsertRawFuturesBatchAsync(Arg.Any<RawFuturesStorageBatch>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var batch = call.Arg<RawFuturesStorageBatch>();
            await db.InsertRawFuturesBatchAsync(batch, call.Arg<CancellationToken>());
            if (fail) { Interlocked.Increment(ref faultCount); throw new IOException("Injected lost completion after Scylla commit"); }
        });
        using (var consumer = new RawFuturesStorageConsumer(options, transport, factory, NullLogger<RawFuturesStorageConsumer>.Instance))
        {
            await consumer.StartAsync(CancellationToken.None);
            await WaitAsync(() => Volatile.Read(ref faultCount) > 0, TimeSpan.FromSeconds(30));
            await consumer.StopAsync(CancellationToken.None);
        }
        fail = false;
        using (var consumer = new RawFuturesStorageConsumer(options, transport, factory, NullLogger<RawFuturesStorageConsumer>.Instance))
        {
            await consumer.StartAsync(CancellationToken.None);
            await WaitAsync(async () =>
            {
                var state = await js.GetConsumerAsync(options.Stream, "raw_rows_v1");
                await state.RefreshAsync();
                return state.Info.NumPending == 0 && state.Info.NumAckPending == 0;
            }, TimeSpan.FromSeconds(60));
            Assert.Equal(HealthStatus.Healthy, (await consumer.CheckHealthAsync(new HealthCheckContext())).Status);
            await consumer.StopAsync(CancellationToken.None);
        }
        using var cluster = Cluster.Builder().AddContactPoint("localhost").WithPort(infrastructure.CqlPort).Build();
        using var session = await cluster.ConnectAsync(infrastructure.CqlKeyspace);
        using var trades = await session.ExecuteAsync(new SimpleStatement("SELECT * FROM tick_trade_data WHERE asset_type_id=1 AND contract_id='ES-raw-test';"));
        var tradeRows = trades.ToArray(); Assert.Equal(5000, tradeRows.Length);
        Assert.Equal(Enumerable.Range(1, 5000).Select(n => (long)n * 2), tradeRows.Select(row => row.GetValue<long>("source_sequence")).Order());
        Assert.Equal(Enumerable.Range(1, 5000).Sum(n => (long)n * 2), tradeRows.Sum(row => row.GetValue<long>("size")));
        var bySequence = tradeRows.ToDictionary(row => row.GetValue<long>("source_sequence"));
        foreach (var originalRecord in original.Records.Where(record => record.Kind == 1))
        {
            var expected = MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(originalRecord.Payload);
            var row = bySequence[expected.TradeData.SourceSequence];
            Assert.Equal(expected.Id, row.GetValue<Guid>("actor_event_id"));
            Assert.Equal(expected.CommandId, row.GetValue<Guid>("command_id"));
            Assert.Equal(expected.TickDataId.TimestampUtc.Ticks, row.GetValue<long>("aggregation_timestamp_utc_ticks"));
            Assert.Equal(expected.TradeData.EventTimestampNanoseconds, row.GetValue<long>("source_event_timestamp_ns"));
            Assert.Equal(expected.TradeData.ReceiveTimestampNanoseconds, row.GetValue<long>("source_receive_timestamp_ns"));
        }
        session.UserDefinedTypes.Define(UdtMap.For<QuoteProof>("tick_quote_item")
            .Map(q => q.Sequence, "source_sequence").Map(q => q.EventNs, "source_event_timestamp_ns").Map(q => q.ReceiveNs, "source_receive_timestamp_ns")
            .Map(q => q.Flags, "header_flags").Map(q => q.BidRaw, "bid_price_raw").Map(q => q.Bid, "bid_price").Map(q => q.BidSize, "bid_size")
            .Map(q => q.BidCount, "bid_count").Map(q => q.AskRaw, "ask_price_raw").Map(q => q.Ask, "ask_price").Map(q => q.AskSize, "ask_size").Map(q => q.AskCount, "ask_count"));
        using var quotes = await session.ExecuteAsync(new SimpleStatement("SELECT quote_count,quote_data FROM tick_quote_data WHERE asset_type_id=1 AND contract_id='ES-raw-test';"));
        var quoteRows = quotes.ToArray(); Assert.Equal(5000, quoteRows.Sum(row => (int)row.GetValue<short>("quote_count")));
        var restoredQuotes = quoteRows.SelectMany(row => row.GetValue<IEnumerable<QuoteProof>>("quote_data")).ToArray();
        Assert.Equal(Enumerable.Range(0, 5000).Select(n => (long)n * 2 + 1), restoredQuotes.Select(q => q.Sequence).Order());
        Assert.All(restoredQuotes, quote =>
        {
            Assert.Equal(6000m, quote.Bid); Assert.Equal(6000.25m, quote.Ask); Assert.Equal(6000000000000, quote.BidRaw);
            Assert.Equal(6000250000000, quote.AskRaw); Assert.Equal(quote.Sequence, quote.BidSize); Assert.Equal(quote.Sequence + 1, quote.AskSize);
            Assert.Equal(1, quote.BidCount); Assert.Equal(2, quote.AskCount); Assert.Equal(7, quote.ReceiveNs - quote.EventNs);
        });
        var window = await db.GetRawFuturesTradeWindowAsync("ES-raw-test", DateOnly.FromDateTime(RawFuturesJournalFixture.At),
            TimeOnly.FromDateTime(RawFuturesJournalFixture.At), TimeOnly.FromDateTime(RawFuturesJournalFixture.At.AddSeconds(1)));
        Assert.Equal(5000, window.Count);
        var exactMillisecond = await db.GetRawFuturesTradeWindowAsync("ES-raw-test", DateOnly.FromDateTime(RawFuturesJournalFixture.At),
            TimeOnly.FromDateTime(RawFuturesJournalFixture.At), TimeOnly.FromDateTime(RawFuturesJournalFixture.At));
        Assert.Equal(5000, exactMillisecond.Count); // Existing endpoint/key precision survives the new writer.
        await new TradeTickQuoteSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Retired_partial_records_and_corrupt_cursors_block_instead_of_skipping()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-raw-gaps-" + Guid.NewGuid().ToString("N"));
        var original = RawFuturesJournalFixture.Batch(2, true);
        var path = RawFuturesJournalFixture.Write(root, original);
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Write)) file.SetLength(file.Length - 5);
        await Assert.ThrowsAsync<InvalidDataException>(() => RawFuturesJournal.ReadAsync(path, 64, CancellationToken.None));
        path = RawFuturesJournalFixture.Write(root, original);
        File.WriteAllBytes(path + ".cursor", new byte[64]);
        await Assert.ThrowsAsync<InvalidDataException>(() => RawFuturesJournal.ReadAsync(path, 64, CancellationToken.None));
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Native_export_preserves_original_fields_and_cursors_when_requested()
    {
        var export = Environment.GetEnvironmentVariable("IFM_NATIVE_RAW_FIXTURE");
        if (string.IsNullOrWhiteSpace(export)) return; // Explicit interop qualification supplements the portable generated fixture.
        var root = Path.Combine(Path.GetTempPath(), "ifm-native-interop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach (var source in Directory.EnumerateFiles(export)) File.Copy(source, Path.Combine(root, Path.GetFileName(source)));
        var trades = 0; var quotes = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.wal"))
        {
            while (true)
            {
                var (batch, complete) = await RawFuturesJournal.ReadAsync(path, 64, CancellationToken.None);
                if (batch is not null)
                {
                    foreach (var record in batch.Records)
                    {
                        if (record.Kind == 1)
                        {
                            var value = MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(record.Payload);
                            Assert.Equal((uint)record.Ordinal, value.TradeData.SourceSequence);
                            Assert.Equal(1795467599000000000L + record.Ordinal, value.TradeData.EventTimestampNanoseconds);
                            Assert.Equal(6000m, value.TradeData.Price); Assert.Equal(value.TickDataId.TimestampUtc, value.ReceivedOn); trades++;
                        }
                        else
                        {
                            var value = MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickQuoteDataChangedEvent>(record.Payload);
                            using var lease = value.QuoteData;
                            Assert.Equal((uint)record.Ordinal, lease.Items[0].SourceSequence);
                            Assert.Equal(1795467599000000000L + record.Ordinal, lease.Items[0].EventTimestampNanoseconds);
                            Assert.Equal(6000.25m, lease.Items[0].AskPrice); quotes++;
                        }
                    }
                    RawFuturesJournal.CommitCursor(path, new(batch.SourceEpoch, batch.EndOffset, batch.Records[^1].Ordinal));
                }
                if (complete) break;
            }
        }
        Assert.Equal(10_000, trades); Assert.Equal(10_000, quotes);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, recursive: true);
    }
    static Task WaitAsync(Func<bool> condition, TimeSpan timeout) => WaitAsync(() => Task.FromResult(condition()), timeout);
    public sealed class QuoteProof
    {
        public long Sequence { get; set; } public long EventNs { get; set; } public long ReceiveNs { get; set; }
        public short Flags { get; set; } public long BidRaw { get; set; } public decimal Bid { get; set; }
        public long BidSize { get; set; } public long BidCount { get; set; } public long AskRaw { get; set; }
        public decimal Ask { get; set; } public long AskSize { get; set; } public long AskCount { get; set; }
    }
    static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        while (!await condition()) await Task.Delay(50, deadline.Token);
    }
}
