using System.Diagnostics;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class IronCondorPositionSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("condorsettlement", includeCql: false); await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using var pg = new NpgsqlConnection(cs); await pg.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE plan_receipts(scenario text, command_id uuid, fingerprint text NOT NULL, payload bytea NOT NULL, revision bigint NOT NULL, PRIMARY KEY(scenario,command_id));", pg)) await create.ExecuteNonQueryAsync();
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var kind in new[] { "Position", "Monitoring" })
        foreach (var mode in new[] { "throw", "terminal", "lost-reply", "publication", "completed-no-ack" })
        {
            var name = $"CONDOR_{storage}_{kind}_{mode.Replace('-', '_')}"; var subject = "condorcase." + name;
            await using var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }); var js = new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name, [subject]) { Storage = storage, Retention = StreamConfigRetention.Workqueue, MaxMsgs = 16, MaxBytes = 1024 * 1024, Discard = StreamConfigDiscard.New });
            await js.CreateOrUpdateConsumerAsync(name, new ConsumerConfig("ordered") { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
            await using (var inputs = new IronCondorPositionDeliveryFixture(false))
                foreach (var seq in new[] { 1, 2 }) (await js.PublishAsync(subject, Encode(inputs.Input(kind, seq)), serializer: Serializer)).EnsureSuccess();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(IronCondorPositionSettlementVerification).Assembly.Location);
            foreach (var arg in new[] { "--condor-settlement-crash-child", name, kind, mode }) start.ArgumentList.Add(arg);
            start.Environment["IFM_CONDOR_TEST_NATS"] = infrastructure.NatsUrl; start.Environment["IFM_CONDOR_TEST_PG"] = cs;
            using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); } catch { child.Kill(entireProcessTree: true); throw; }
            Require(child.ExitCode == 0 && (await stdout).Contains("CONDOR_INPUT_RETAINED"), "Condor child failed: " + await stderr);
            await using var recovered = new IronCondorPositionDeliveryFixture(false); Bind(recovered, pg, name); await recovered.Start();
            var consumer = await js.GetConsumerAsync(name, "ordered");
            foreach (var seq in new[] { 1, 2 })
            {
                var input = await Fetch(consumer); var source = Decode(kind, input.Data!); var expected = recovered.Input(kind, seq);
                Require(source.Id == expected.Id && source.CommandId == expected.CommandId && source.ReceivedOn == expected.ReceivedOn, "Position identity/time/order changed.");
                Require(await recovered.Deliver(source), "Unchanged-input position recovery failed.");
                Require(recovered.PublishedPlans.SequenceEqual(Enumerable.Range(1, seq).Select(n => (long)n)), "Recovered revisions were reordered.");
                await input.AckAsync(new AckOpts { DoubleAck = true });
            }
            await consumer.RefreshAsync(); Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Position sources remain pending.");
            await using var count = new NpgsqlCommand("SELECT count(*),min(revision),max(revision) FROM plan_receipts WHERE scenario=@scenario", pg); count.Parameters.AddWithValue("scenario", name);
            await using (var rows = await count.ExecuteReaderAsync())
            { await rows.ReadAsync(); Require(rows.GetInt64(0) == 2 && rows.GetInt64(1) == 1 && rows.GetInt64(2) == 2, "Plan receipts duplicated or lost."); }
            Console.WriteLine($"PASS {storage}/{kind}/{mode}: original source, command IDs and unchanged fingerprints; ordered revisions 1,2; two PostgreSQL surrogate receipts; no pending input");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Actual position actor/scheduler and command construction. Function calculations, pricing/cache and publications substituted; PostgreSQL stores surrogate command receipts, not production financial rows. Fixed test captures/clock qualify unchanged-input recovery only. PositionChanged recapture drift remains a separately reproduced blocker. Broker enforces one input in flight and remains up during API-child exit; no production JetStream adapter or broker/power-loss proof.");
    }
    static void Bind(IronCondorPositionDeliveryFixture fixture, NpgsqlConnection pg, string scenario)
    {
        fixture.Commit = async (command, completed) =>
        {
            await using (var insert = new NpgsqlCommand("INSERT INTO plan_receipts(scenario,command_id,fingerprint,payload,revision) VALUES(@scenario,@id,@fingerprint,@payload,@revision) ON CONFLICT DO NOTHING", pg))
            {
                insert.Parameters.AddWithValue("scenario", scenario); insert.Parameters.AddWithValue("id", command.CommandId); insert.Parameters.AddWithValue("fingerprint", command.Fingerprint());
                insert.Parameters.AddWithValue("payload", MessagePackSerializer.Serialize(completed)); insert.Parameters.AddWithValue("revision", completed.Plan.PlanRevision); await insert.ExecuteNonQueryAsync();
            }
            await using var read = new NpgsqlCommand("SELECT payload FROM plan_receipts WHERE scenario=@scenario AND command_id=@id", pg); read.Parameters.AddWithValue("scenario", scenario); read.Parameters.AddWithValue("id", command.CommandId);
            var original = MessagePackSerializer.Deserialize<IronCondorTradePlanUpdatedEvent>((byte[])(await read.ExecuteScalarAsync())!);
            Require(original.RequestFingerprint == command.Fingerprint(), "Same deterministic command changed its captured inputs."); return original;
        };
    }
    public static async Task CrashChild(string name, string kind, string mode)
    {
        var pg = new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_CONDOR_TEST_PG")); await pg.OpenAsync();
        var fixture = new IronCondorPositionDeliveryFixture(false) { Failure = mode == "completed-no-ack" ? "none" : mode }; Bind(fixture, pg, name); await fixture.Start();
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_CONDOR_TEST_NATS")! }); var consumer = await new NatsJSContext(connection).GetConsumerAsync(name, "ordered");
        var input = await Fetch(consumer); var source = Decode(kind, input.Data!);
        Require(await fixture.Deliver(source) == (mode == "completed-no-ack"), "Unexpected position child settlement.");
        var overtook = 0;
        await foreach (var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) if (Decode(kind, probe.Data!).Id != source.Id) overtook++;
        Require(overtook == 0, "Successor overtook the original position source.");
        Console.WriteLine("CONDOR_INPUT_RETAINED"); Environment.Exit(0); // No ACK or actor/connection disposal.
    }
    static byte[] Encode(IEvent source) => source switch { IronCondorPositionChangedEvent p => MessagePackSerializer.Serialize(p), IronCondorMonitoringInputsChangedEvent m => MessagePackSerializer.Serialize(m), _ => throw new InvalidDataException() };
    static IEvent Decode(string kind, byte[] bytes) => kind == "Position" ? MessagePackSerializer.Deserialize<IronCondorPositionChangedEvent>(bytes) : MessagePackSerializer.Deserialize<IronCondorMonitoringInputsChangedEvent>(bytes);
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected position input was not redelivered.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
