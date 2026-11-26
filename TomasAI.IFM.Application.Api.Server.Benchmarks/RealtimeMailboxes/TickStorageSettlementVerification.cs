using System.Diagnostics;
using System.Globalization;
using Cassandra;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class TickStorageSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        var previousDev = Environment.GetEnvironmentVariable("SCYLLADB_DEV_KEY"); var previousTest = Environment.GetEnvironmentVariable("SCYLLADB_TEST_KEY");
        Environment.SetEnvironmentVariable("SCYLLADB_DEV_KEY", "{\"userid\":\"cassandra\",\"password\":\"cassandra\"}");
        Environment.SetEnvironmentVariable("SCYLLADB_TEST_KEY", "{\"userid\":\"cassandra\",\"password\":\"cassandra\"}");
        try
        {
            await using var infrastructure = new IsolatedIntegrationInfrastructure("ticksettlement", includeCql: true); await infrastructure.StartAsync();
            var settings = Settings(infrastructure.CqlConnectionString);
            await new TradeTickQuoteSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
            using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(infrastructure.CqlPort).Build();
            using var read = await cluster.ConnectAsync(infrastructure.CqlKeyspace);
            read.UserDefinedTypes.Define(UdtMap.For<ReadQuoteItem>("tick_quote_item")
                .Map(x => x.SourceSequence, "source_sequence").Map(x => x.EventTime, "source_event_timestamp_ns")
                .Map(x => x.ReceiveTime, "source_receive_timestamp_ns").Map(x => x.Flags, "header_flags")
                .Map(x => x.BidRaw, "bid_price_raw").Map(x => x.BidPrice, "bid_price").Map(x => x.BidSize, "bid_size").Map(x => x.BidCount, "bid_count")
                .Map(x => x.AskRaw, "ask_price_raw").Map(x => x.AskPrice, "ask_price").Map(x => x.AskSize, "ask_size").Map(x => x.AskCount, "ask_count"));
            foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
            foreach (var kind in new[] { "Trade", "Quote" })
            foreach (var mode in new[] { "refused", "throw", "lost-reply", "completed-no-ack" })
            {
                var name = $"TICK_{storage}_{kind}_{mode.Replace('-', '_')}"; var subject = "tick." + name;
                await using var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }); var js = new NatsJSContext(connection);
                await js.CreateStreamAsync(new StreamConfig(name, [subject]) { Storage = storage, Retention = StreamConfigRetention.Workqueue, MaxMsgs = 16, MaxBytes = 1024 * 1024, Discard = StreamConfigDiscard.New });
                await js.CreateOrUpdateConsumerAsync(name, new ConsumerConfig("ordered") { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
                foreach (var seq in new[] { 1, 2 }) (await js.PublishAsync(subject, TickStorageDeliveryFixture.Encode(TickStorageDeliveryFixture.Input(kind, seq, name)), serializer: Serializer)).EnsureSuccess();
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(typeof(TickStorageSettlementVerification).Assembly.Location);
                foreach (var arg in new[] { "--tick-settlement-crash-child", name, kind, mode }) start.ArgumentList.Add(arg);
                start.Environment["IFM_TICK_TEST_NATS"] = infrastructure.NatsUrl; start.Environment["IFM_TICK_TEST_CQL"] = infrastructure.CqlConnectionString;
                using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); } catch { child.Kill(entireProcessTree: true); throw; }
                Require(child.ExitCode == 0 && (await stdout).Contains("TICK_INPUT_RETAINED"), "Tick child failed: " + await stderr);
                await using var fixture = Bind(infrastructure.CqlConnectionString, "none"); await fixture.Start();
                var consumer = await js.GetConsumerAsync(name, "ordered");
                foreach (var seq in new[] { 1, 2 })
                {
                    var input = await Fetch(consumer); var source = TickStorageDeliveryFixture.Decode(kind, input.Data!);
                    Require(source.Id == TickStorageDeliveryFixture.Input(kind, seq, name).Id, "Tick identity/order changed on redelivery.");
                    Require(await fixture.Deliver(source), "Tick recovery failed."); await input.AckAsync(new AckOpts { DoubleAck = true });
                }
                await consumer.RefreshAsync(); Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Pending tick sources remain.");
                var table = kind == "Trade" ? "tick_trade_data" : "tick_quote_data";
                var rows = (await read.ExecuteAsync(new SimpleStatement($"SELECT * FROM {table} WHERE asset_type_id=? AND contract_id=?", (sbyte)AssetTypeId.Futures, name))).ToArray();
                Require(rows.Length == 2 && rows.Select(r => r.GetValue<long>("sequence_id")).SequenceEqual(new long[] { 1, 2 }), "Scylla rows duplicated, lost or reordered.");
                foreach (var row in rows)
                {
                    var seq = (int)row.GetValue<long>("sequence_id"); var expected = TickStorageDeliveryFixture.Input(kind, seq, name);
                    Require(row.GetValue<Guid>("actor_event_id") == expected.Id && row.GetValue<Guid>("command_id") == expected.CommandId, "Stored source/causation changed.");
                    Require(row.GetValue<DateTimeOffset>("received_on").UtcDateTime == expected.ReceivedOn, "Stored observation timestamp changed.");
                    if (kind == "Trade") Require(decimal.Parse(row["price"].ToString()!, CultureInfo.InvariantCulture) == 5450.25m, "Trade price changed.");
                    else
                    {
                        var quotes = row.GetValue<IEnumerable<ReadQuoteItem>>("quote_data").ToArray();
                        Require(row.GetValue<short>("quote_count") == 2 && quotes.Length == 2
                            && decimal.Parse(quotes[0].BidPrice.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) == 5450m
                            && decimal.Parse(quotes[1].AskPrice.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) == 5450.5m, "Stored quote prefix or prices changed.");
                    }
                }
                Console.WriteLine($"PASS {storage}/{kind}/{mode}: original input ordered after abrupt exit, exact two Scylla rows, original timestamps and prices");
                await js.DeleteStreamAsync(name);
            }
            Console.WriteLine("Actual actor/scheduler and production TradeTickQuoteDb schema/write methods on isolated ScyllaDB. Projector control/publication is substituted to inject refusal, exception and lost response. Broker enforces one input in flight; fresh quote payloads are decoded per retry. Production JetStream adapter, source/complete fanout, broker loss and power loss are not qualified.");
        }
        finally { Environment.SetEnvironmentVariable("SCYLLADB_DEV_KEY", previousDev); Environment.SetEnvironmentVariable("SCYLLADB_TEST_KEY", previousTest); }
    }
    static IDbConnectionSettings Settings(string connection) => new DbConnectionSettings().Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection, connection, "System.Data.ScyllaDb");
    static TickStorageDeliveryFixture Bind(string connection, string mode)
    {
        var store = new TradeTickQuoteDbContext(Settings(connection), NullLogger<DbProvider>.Instance);
        return new(false)
        {
            DeliveryTimeout = TimeSpan.FromSeconds(30),
            Failure = mode == "completed-no-ack" ? "none" : mode,
            Persist = source => source switch
            {
                FuturesTickTradeDataInsertedEvent trade => store.InsertTickTradeDataAsync(trade),
                FuturesTickQuoteDataInsertedEvent quote => store.InsertTickQuoteDataAsync(quote),
                _ => throw new InvalidDataException("Unexpected storage effect.")
            }
        };
    }
    public static async Task CrashChild(string name, string kind, string mode)
    {
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_TICK_TEST_NATS")! });
        var consumer = await new NatsJSContext(connection).GetConsumerAsync(name, "ordered");
        var fixture = Bind(Environment.GetEnvironmentVariable("IFM_TICK_TEST_CQL")!, mode); await fixture.Start();
        var input = await Fetch(consumer); var source = TickStorageDeliveryFixture.Decode(kind, input.Data!);
        Require(await fixture.Deliver(source) == (mode == "completed-no-ack"), "Unexpected child tick settlement.");
        var overtook = 0;
        await foreach (var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer))
        {
            var repeated = TickStorageDeliveryFixture.Decode(kind, probe.Data!);
            if (repeated.Id != source.Id) overtook++;
            if (repeated is FuturesTickQuoteDataChangedEvent quote) quote.QuoteData.Dispose();
        }
        Require(overtook == 0, "Later tick source overtook its unacknowledged predecessor.");
        Console.WriteLine("TICK_INPUT_RETAINED"); Environment.Exit(0); // No ACK and no actor/client disposal.
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected retained tick input was unavailable.");
    }
    static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    public sealed class ReadQuoteItem
    {
        public long SourceSequence { get; set; }
        public long EventTime { get; set; }
        public long ReceiveTime { get; set; }
        public short Flags { get; set; }
        public long BidRaw { get; set; }
        public decimal BidPrice { get; set; }
        public long BidSize { get; set; }
        public long BidCount { get; set; }
        public long AskRaw { get; set; }
        public decimal AskPrice { get; set; }
        public long AskSize { get; set; }
        public long AskCount { get; set; }
    }
}
