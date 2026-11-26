using System.Diagnostics;
using MessagePack;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class ItiSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    static readonly MessagePackSerializerOptions EnvelopeOptions = MessagePackSerializerOptions.Standard.WithResolver(
        MessagePack.Resolvers.CompositeResolver.Create(ItiSettlementTestResolver.Instance, MessagePack.Resolvers.StandardResolver.Instance));
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("itisettlement", includeCql: false);
        await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
        { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        using var pg = new NpgsqlConnection(cs); pg.Open();
        using (var create = new NpgsqlCommand("CREATE TABLE iti_receipts (scenario text NOT NULL, id uuid NOT NULL, ordinal int NOT NULL, payload bytea NOT NULL, PRIMARY KEY(scenario,id));", pg)) create.ExecuteNonQuery();
        var results = new List<object>();
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var mode in new[] { "refused", "throw", "lost-reply", "completed-no-ack" })
        {
            var name = storage + "_" + mode.Replace('-', '_'); var stream = "ITI_" + name;
            var subject = "iti." + name;
            var root = Path.Combine(Path.GetTempPath(), "IFM-iti-broker-" + Guid.NewGuid().ToString("N"));
            await using (var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }))
            {
                var js = new NatsJSContext(connection);
                await js.CreateStreamAsync(new StreamConfig(stream, [subject])
                { Storage = storage, Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New, MaxMsgs = 16, MaxBytes = 1024 * 1024, NumReplicas = 1 });
                await js.CreateOrUpdateConsumerAsync(stream, new ConsumerConfig("ordered")
                { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
                for (var seq = 1; seq <= 2; seq++)
                    (await js.PublishAsync(subject, Encode(ItiDeliveryFixture.Source(seq, seq == 1 ? 100 : 105)), serializer: Serializer)).EnsureSuccess();
            }
            await Child(mode, stream, root, infrastructure.NatsUrl, cs);
            var effects = new ItiDeliveryFixture.Effects();
            using (var select = new NpgsqlCommand("SELECT payload FROM iti_receipts WHERE scenario=@scenario ORDER BY ordinal", pg))
            {
                select.Parameters.AddWithValue("scenario", name); using var read = select.ExecuteReader();
                while (read.Read()) effects.Apply(FuturesItiJournalEnvelope.Decode(read.GetFieldValue<byte[]>(0), 0));
            }
            using var recovery = new FuturesItiIngressRecovery(root);
            await using var fixture = new ItiDeliveryFixture(recovery, effects) { Vx = 99 };
            effects.WriteReceipt = command => Write(pg, name, command);
            ItiDeliveryFixture.SeedAtr(99);
            await fixture.Start(); // Cold recovery with no new source event, even though current VX/ATR changed.
            Require(recovery.PendingCount == 0, "Cold recovery left pending work.");
            if (mode != "completed-no-ack")
            {
                var original = effects.Attempts.Single();
                Require(original.CommandId == ItiDeliveryFixture.Source(1).Id && original.VixFuturesPrice == 20 && original.DailyAtr?.Value == 8
                    && original.Timestamp == ItiDeliveryFixture.At.AddTicks(1), "Recovery resampled original command inputs.");
            }
            else Require(effects.Attempts.Count == 0, "Completed input was dispatched twice during cold recovery.");
            await using (var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }))
            {
                var js = new NatsJSContext(connection); var consumer = await js.GetConsumerAsync(stream, "ordered");
                foreach (var expected in new[] { 1, 2 })
                {
                    var input = await Fetch(consumer);
                    var source = Decode(input.Data!);
                    Require(source.Id == ItiDeliveryFixture.Source(expected).Id, "Broker source order or identity changed.");
                    Require(await fixture.Deliver(source), "Recovered input failed settlement.");
                    await input.AckAsync(new AckOpts { DoubleAck = true });
                }
                await consumer.RefreshAsync();
                Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Broker backlog was not drained.");
                await js.DeleteStreamAsync(stream);
            }
            Require(effects.Committed.Select(c => c.CommandId).SequenceEqual(new[] { ItiDeliveryFixture.Source(1).Id, ItiDeliveryFixture.Source(2).Id }), "Command order changed.");
            Require(effects.Changes == 2 && effects.State.FuturesItiSignal!.Threshold == 8, "C# ITI result changed or duplicated on retry.");
            using var count = new NpgsqlCommand("SELECT COUNT(*) FROM iti_receipts WHERE scenario=@scenario", pg); count.Parameters.AddWithValue("scenario", name);
            Require((long)count.ExecuteScalar()! == 2, "Duplicate or missing durable command receipt.");
            results.Add(new { storage = storage.ToString(), mode, authenticOriginalInputsPreserved = true,
                ordered = true, abruptProcessRecovery = true, uniqueCommandReceipts = 2, realCSharpSignalChanges = effects.Changes,
                originalThreshold = effects.State.FuturesItiSignal.Threshold });
            Console.WriteLine("PASS " + name);
        }
        Console.WriteLine(JsonSerializer.Serialize(new { results, scope = "Real ITI ingress, actor/scheduler and C# compute; physical input journal, disposable NATS and PostgreSQL command-receipt surrogate. Domain command actor/store/projector chain and production JetStream adapter are not exercised end-to-end. NATS remains running during child exit." }));
    }
    [MessagePackObject]
    public sealed record TestEnvelope([property: Key(0)] FuturesMarketPriceUpdatedRealtimeEvent Source,
        [property: Key(1)] string Dataset, [property: Key(2)] Guid Generation);
    static byte[] Encode(FuturesMarketPriceUpdatedRealtimeEvent source)
        => MessagePackSerializer.Serialize(new TestEnvelope(source, source.SourceDataset, source.SourceGenerationId), EnvelopeOptions);
    static FuturesMarketPriceUpdatedRealtimeEvent Decode(byte[] data)
    {
        var value = MessagePackSerializer.Deserialize<TestEnvelope>(data, EnvelopeOptions);
        return value.Source with { SourceDataset = value.Dataset, SourceGenerationId = value.Generation };
    }
    static void Write(NpgsqlConnection pg, string scenario, TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands.GenerateFuturesItiSignalCommand command)
    {
        using var write = new NpgsqlCommand("INSERT INTO iti_receipts VALUES (@scenario,@id,@ordinal,@payload) ON CONFLICT DO NOTHING", pg);
        write.Parameters.AddWithValue("scenario", scenario); write.Parameters.AddWithValue("id", command.CommandId);
        write.Parameters.AddWithValue("ordinal", BitConverter.ToInt32(command.CommandId.ToByteArray(), 0));
        write.Parameters.AddWithValue("payload", FuturesItiJournalEnvelope.Encode(command)); write.ExecuteNonQuery();
    }
    public static async Task CrashChild(string mode, string stream, string root)
    {
        var pg = new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_ITI_TEST_PG")); pg.Open();
        var effects = new ItiDeliveryFixture.Effects { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply" };
        effects.WriteReceipt = command => Write(pg, stream[4..], command);
        var recovery = new FuturesItiIngressRecovery(root); var fixture = new ItiDeliveryFixture(recovery, effects); await fixture.Start();
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_ITI_TEST_NATS")! });
        var consumer = await new NatsJSContext(connection).GetConsumerAsync(stream, "ordered");
        var input = await Fetch(consumer); var source = Decode(input.Data!);
        Require(await fixture.Deliver(source) == (mode == "completed-no-ack"), "Unexpected child settlement.");
        var overtook = 0;
        await foreach (var _ in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) overtook++;
        Require(overtook == 0, "Later broker input overtook unacknowledged input.");
        Console.WriteLine("ITI_ORIGINAL_INPUT_RETAINED"); Environment.Exit(0); // No actor, input-journal, broker, or DB disposal, and no source ACK.
    }
    static async Task Child(string mode, string stream, string root, string url, string cs)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(ItiSettlementVerification).Assembly.Location);
        foreach (var arg in new[] { "--iti-settlement-crash-child", mode, stream, root }) start.ArgumentList.Add(arg);
        start.Environment["IFM_ITI_TEST_NATS"] = url; start.Environment["IFM_ITI_TEST_PG"] = cs;
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(entireProcessTree: true); throw; }
        Require(process.ExitCode == 0 && (await stdout).Contains("ITI_ORIGINAL_INPUT_RETAINED", StringComparison.Ordinal), "ITI crash child failed: " + await stderr);
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected ITI broker input was not available.");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}

[GeneratedMessagePackResolver]
internal partial class ItiSettlementTestResolver;
