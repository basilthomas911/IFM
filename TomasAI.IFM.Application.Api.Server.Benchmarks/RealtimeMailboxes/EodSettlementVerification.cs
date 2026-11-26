using System.Diagnostics;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class EodSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("eodsettlement", includeCql: false); await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using var pg = new NpgsqlConnection(cs); await pg.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE eod_state (scope text PRIMARY KEY, payload bytea NOT NULL, writes int NOT NULL DEFAULT 0);", pg)) await create.ExecuteNonQueryAsync();
        var results = new List<object>();
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var workers in new[] { false, true })
        foreach (var mode in new[] { "refused", "throw", "lost-reply", "completed-no-ack" })
        {
            var scope = $"{storage}_{workers}_{mode.Replace('-', '_')}"; var stream = "EOD_" + scope; var subject = "eod." + scope;
            await using (var insert = new NpgsqlCommand("INSERT INTO eod_state(scope,payload) VALUES (@scope,@payload)", pg))
            { insert.Parameters.AddWithValue("scope", scope); insert.Parameters.AddWithValue("payload", MessagePackBinarySerializer.Shared.Serialize(EodDeliveryFixture.Initial())!); await insert.ExecuteNonQueryAsync(); }
            await using (var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }))
            {
                var js = new NatsJSContext(connection);
                await js.CreateStreamAsync(new StreamConfig(stream, [subject]) { Storage = storage, Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New, MaxMsgs = 16, MaxBytes = 1024 * 1024, NumReplicas = 1 });
                await js.CreateOrUpdateConsumerAsync(stream, new ConsumerConfig("ordered") { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
                for (var seq = 1; seq <= 2; seq++) (await js.PublishAsync(subject, MessagePackBinarySerializer.Shared.Serialize(EodDeliveryFixture.Source(seq))!, serializer: Serializer)).EnsureSuccess();
            }
            await Child(mode, stream, workers, infrastructure.NatsUrl, cs);
            await using var fixture = new EodDeliveryFixture(workers); Bind(fixture, pg, scope); await fixture.Start();
            await using (var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }))
            {
                var js = new NatsJSContext(connection); var consumer = await js.GetConsumerAsync(stream, "ordered");
                foreach (var expected in new[] { 1, 2 })
                {
                    var input = await Fetch(consumer); var source = MessagePackBinarySerializer.Shared.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(input.Data!)!;
                    Require(source.Id == EodDeliveryFixture.Source(expected).Id, "Original EOD source identity/order changed.");
                    Require(await fixture.Deliver(source), "Recovered EOD input failed settlement.");
                    await input.AckAsync(new AckOpts { DoubleAck = true });
                }
                await consumer.RefreshAsync(); Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Pending EOD input remains."); await js.DeleteStreamAsync(stream);
            }
            var current = await fixture.ReadRow!();
            await using var count = new NpgsqlCommand("SELECT writes FROM eod_state WHERE scope=@scope", pg); count.Parameters.AddWithValue("scope", scope);
            var writes = (int)(await count.ExecuteScalarAsync())!;
            Require(current.Volume == 30 && current.OpenPrice == 97 && current.HighPrice == 115 && writes == 2, "EOD rows duplicated, lost or reordered.");
            results.Add(new { storage = storage.ToString(), workers, mode, abruptProcessRecovery = true, ordered = true, writes, finalVolume = current.Volume }); Console.WriteLine("PASS " + scope);
        }
        Console.WriteLine(JsonSerializer.Serialize(new { results, scope = "Actual EOD handlers/actor/scheduler and C# statistics math. Disposable NATS retains unacknowledged inputs; PostgreSQL stores surrogate serialized EOD snapshots. Production buffered projector, MarketDataDb schema and publication recovery are not qualified. Broker remains running." }));
    }
    static void Bind(EodDeliveryFixture fixture, NpgsqlConnection pg, string scope)
    {
        fixture.ReadRow = async () =>
        {
            await using var query = new NpgsqlCommand("SELECT payload FROM eod_state WHERE scope=@scope", pg); query.Parameters.AddWithValue("scope", scope);
            return MessagePackBinarySerializer.Shared.Deserialize<FuturesEodDataV2ReadModel>((byte[])(await query.ExecuteScalarAsync())!)!;
        };
        fixture.WriteRow = async row =>
        {
            await using var write = new NpgsqlCommand("UPDATE eod_state SET payload=@payload,writes=writes+1 WHERE scope=@scope", pg);
            write.Parameters.AddWithValue("scope", scope); write.Parameters.AddWithValue("payload", MessagePackBinarySerializer.Shared.Serialize(row)!); await write.ExecuteNonQueryAsync();
        };
    }
    public static async Task CrashChild(string mode, string stream, bool workers)
    {
        var pg = new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_EOD_TEST_PG")); await pg.OpenAsync();
        var fixture = new EodDeliveryFixture(workers) { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply" }; Bind(fixture, pg, stream[4..]); await fixture.Start();
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_EOD_TEST_NATS")! });
        var consumer = await new NatsJSContext(connection).GetConsumerAsync(stream, "ordered"); var input = await Fetch(consumer);
        var source = MessagePackBinarySerializer.Shared.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(input.Data!)!;
        Require(await fixture.Deliver(source) == (mode == "completed-no-ack"), "Unexpected child settlement.");
        var overtook = 0;
        await foreach (var _ in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) overtook++;
        Require(overtook == 0, "Later EOD input overtook unacknowledged predecessor.");
        Console.WriteLine("EOD_INPUT_RETAINED"); Environment.Exit(0); // No source ACK and no actor/broker/DB disposal.
    }
    static async Task Child(string mode, string stream, bool workers, string url, string cs)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(EodSettlementVerification).Assembly.Location);
        foreach (var arg in new[] { "--eod-settlement-crash-child", mode, stream, workers.ToString() }) start.ArgumentList.Add(arg);
        start.Environment["IFM_EOD_TEST_NATS"] = url; start.Environment["IFM_EOD_TEST_PG"] = cs;
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(entireProcessTree: true); throw; }
        Require(process.ExitCode == 0 && (await stdout).Contains("EOD_INPUT_RETAINED", StringComparison.Ordinal), "EOD crash child failed: " + await stderr);
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected EOD input was unavailable.");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
