using System.Text.Json;
using System.Diagnostics;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class IndicatorSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("indicatorsettlement", includeCql: false);
        await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
        { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using var pg = new NpgsqlConnection(cs); await pg.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE command_receipts (scenario text NOT NULL, ordinal bigint NOT NULL, PRIMARY KEY(scenario, ordinal));", pg))
            await create.ExecuteNonQueryAsync();
        var results = new List<object>();
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var kind in IndicatorDeliveryFixture.Kinds)
        foreach (var mode in new[] { "refused", "throw", "lost-reply" })
        {
            var scenario = $"{storage}_{kind}_{mode.Replace('-', '_')}";
            var stream = "INDICATOR_" + scenario;
            var subject = "indicator." + scenario;
            await using (var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }))
            {
                var js = new NatsJSContext(connection);
                await js.CreateStreamAsync(new StreamConfig(stream, [subject])
                { Storage = storage, Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New,
                    MaxMsgs = 16, MaxBytes = 1024 * 1024, NumReplicas = 1 });
                var consumer = await js.CreateOrUpdateConsumerAsync(stream, new ConsumerConfig("ordered")
                { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1,
                    AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
                await using var runtime = new IndicatorDeliveryFixture(kind); await runtime.Start();
                for (var seq = 1; seq <= 2; seq++)
                {
                    var source = runtime.Input(seq).Source;
                    var bytes = source switch
                    {
                        FuturesMarketPriceUpdatedRealtimeEvent price => MessagePackBinarySerializer.Shared.Serialize(price)!,
                        FuturesTradeSessionBarClosedRealtimeEvent bar => MessagePackBinarySerializer.Shared.Serialize(bar)!,
                        _ => throw new InvalidDataException("Unknown indicator source.")
                    };
                    (await js.PublishAsync(subject, bytes, serializer: Serializer)).EnsureSuccess();
                }
            }
            await RunCrashChild(kind, mode, stream, infrastructure.NatsUrl, cs);
            // A new command-service substitute restores only persisted receipts, not the old actor or state object.
            var state = new IndicatorDeliveryFixture.State { WriteCommandReceipt = WriteReceipt };
            await using (var read = new NpgsqlCommand("SELECT ordinal FROM command_receipts WHERE scenario=@scenario ORDER BY ordinal", pg))
            {
                read.Parameters.AddWithValue("scenario", scenario);
                await using var rows = await read.ExecuteReaderAsync();
                while (await rows.ReadAsync()) state.Committed.Add(rows.GetInt64(0));
            }
            await using (var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }))
            {
                var js = new NatsJSContext(connection);
                var consumer = await js.GetConsumerAsync(stream, "ordered");
                await using var runtime = new IndicatorDeliveryFixture(kind, state); await runtime.Start();
                foreach (var expected in new long[] { 1, 2 })
                {
                    var input = await Fetch(consumer);
                    var message = Decode(input.Data!, kind);
                    Require(message.Source.Id == runtime.Input(expected).Source.Id, scenario + ": replay identity/order changed");
                    Require(await runtime.Deliver(message), scenario + ": recovery failed");
                    await input.AckAsync(new AckOpts { DoubleAck = true });
                }
                await consumer.RefreshAsync();
                Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, scenario + ": pending work left");
                Require(state.Committed.SequenceEqual(new long[] { 1, 2 }) && state.Attempts.SequenceEqual(new long[] { 1, 2 }), scenario + ": results changed");
                await js.DeleteStreamAsync(stream);
            }
            results.Add(new { storage = storage.ToString(), kind, mode, originalAttempts = new long[] { 1 },
                replayAttempts = state.Attempts, committed = state.Committed,
                failedInputNotAcked = true, noOvertaking = true, recoveredAfterAbruptProcessExit = true });
            Console.WriteLine($"PASS {scenario}");
            void WriteReceipt(long seq)
            {
                using var command = new NpgsqlCommand("INSERT INTO command_receipts VALUES (@scenario,@ordinal) ON CONFLICT DO NOTHING", pg);
                command.Parameters.AddWithValue("scenario", scenario); command.Parameters.AddWithValue("ordinal", seq);
                command.ExecuteNonQuery();
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { results, scope = "Actual indicator actors and scheduler, NATS Memory/File redelivery, abrupt child-process exit and fresh actor/scheduler/connection, PostgreSQL-backed synthetic command receipts. Downstream domain commands and production mailbox adapter are substituted. No broker or OS restart claim." }));
    }
    static async Task RunCrashChild(string kind, string mode, string stream, string url, string cs)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(IndicatorSettlementVerification).Assembly.Location);
        foreach (var arg in new[] { "--indicator-settlement-crash-child", kind, mode, stream }) start.ArgumentList.Add(arg);
        start.Environment["IFM_INDICATOR_TEST_NATS"] = url;
        start.Environment["IFM_INDICATOR_TEST_PG"] = cs;
        using var process = Process.Start(start) ?? throw new IOException("Cannot start isolated indicator crash child.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        Require(process.ExitCode == 0 && (await stdout).Contains("FAILED_INPUT_RETAINED_NO_OVERTAKING", StringComparison.Ordinal),
            "Abrupt indicator child failed: " + await stderr);
    }
    public static async Task CrashChild(string kind, string mode, string stream)
    {
        using var pg = new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_INDICATOR_TEST_PG")); pg.Open();
        var scenario = stream["INDICATOR_".Length..];
        var state = new IndicatorDeliveryFixture.State
        { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply" };
        state.WriteCommandReceipt = seq =>
        {
            using var command = new NpgsqlCommand("INSERT INTO command_receipts VALUES (@scenario,@ordinal) ON CONFLICT DO NOTHING", pg);
            command.Parameters.AddWithValue("scenario", scenario); command.Parameters.AddWithValue("ordinal", seq);
            command.ExecuteNonQuery();
        };
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_INDICATOR_TEST_NATS")! });
        var consumer = await new NatsJSContext(connection).GetConsumerAsync(stream, "ordered");
        var runtime = new IndicatorDeliveryFixture(kind, state); await runtime.Start();
        var input = await Fetch(consumer);
        Require(!await runtime.Deliver(Decode(input.Data!, kind)), "Failed child input was ACKed.");
        Require(state.Attempts.SequenceEqual(new long[] { 1 }), "Unexpected child command sequence.");
        var overtook = 0;
        await foreach (var _ in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts
            { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) overtook++;
        Require(overtook == 0, "Second input overtook failed input.");
        Console.WriteLine("FAILED_INPUT_RETAINED_NO_OVERTAKING");
        Environment.Exit(0); // Deliberately skip actor/pool, broker connection and database disposal; no input ACK.
    }
    static IndicatorDeliveryFixture.Message Decode(byte[] bytes, string kind)
    {
        IEvent source = kind is "Vwap" or "VxTermStructure"
            ? MessagePackBinarySerializer.Shared.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(bytes)!
            : MessagePackBinarySerializer.Shared.Deserialize<FuturesTradeSessionBarClosedRealtimeEvent>(bytes)!;
        return new(source, kind);
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts
            { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected broker input was not available.");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
