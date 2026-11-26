using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command;
using System.Diagnostics;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class DailyFuturesExitIntentVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("dailyexitintent", includeCql: false); await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using var pg = new NpgsqlConnection(cs); await pg.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE plan_receipts(scenario text, command_id uuid, fingerprint text NOT NULL, payload bytea NOT NULL, revision bigint NOT NULL, PRIMARY KEY(scenario,command_id)); CREATE TABLE exit_receipts(ordinal bigserial, scenario text NOT NULL, command_id uuid NOT NULL, payload bytea NOT NULL, started bytea NOT NULL, revision bigint NOT NULL, PRIMARY KEY(scenario,command_id));", pg)) await create.ExecuteNonQueryAsync();
        var intentRoot = Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety", "daily-exit-intent-captures-" + Guid.NewGuid().ToString("N"));
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var kind in new[] { "Position", "Clock" })
        foreach (var mode in new[] { "exit-send", "exit-ambiguous", "before-owner-crash", "after-owner-crash", "completed-no-ack" })
        {
            var name = $"DAILY_EXIT_{storage}_{kind}_{mode.Replace('-', '_')}"; var subject = "dailyexitcase." + name;
            await using var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }); var js = new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name, [subject]) { Storage = storage, Retention = StreamConfigRetention.Workqueue, MaxMsgs = 16, MaxBytes = 1024 * 1024, Discard = StreamConfigDiscard.New });
            await js.CreateOrUpdateConsumerAsync(name, new ConsumerConfig("ordered") { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
            await using (var inputs = new FuturesPositionDeliveryFixture(false))
                foreach (var seq in new[] { 1, 2 }) (await js.PublishAsync(subject, Encode(FuturesPositionDeliveryFixture.DailyInput(kind, seq)), serializer: Serializer)).EnsureSuccess();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(DailyFuturesExitIntentVerification).Assembly.Location);
            foreach (var arg in new[] { "--daily-exit-intent-crash-child", name, kind, mode }) start.ArgumentList.Add(arg);
            start.Environment["IFM_DAILY_EXIT_TEST_DIRECTORY"] = Path.Combine(intentRoot,name);
            start.Environment["IFM_DAILY_EXIT_TEST_NATS"] = infrastructure.NatsUrl; start.Environment["IFM_DAILY_EXIT_TEST_PG"] = cs;
            using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); } catch { child.Kill(entireProcessTree: true); throw; }
            Require(child.ExitCode == 0 && (await stdout).Contains("DAILY_EXIT_INPUT_RETAINED"), "Futures child failed: " + await stderr);
            await using var recovered = new FuturesPositionDeliveryFixture(false, intentDirectory: Path.Combine(intentRoot,name)) { RecoveryPosition = FuturesPositionDeliveryFixture.DailyInput(kind,2).PositionSnapshot }; recovered.Time.Now += TimeSpan.FromDays(1); Bind(recovered, pg, name); await recovered.Start();
            var consumer = await js.GetConsumerAsync(name, "ordered");
            foreach (var seq in new[] { 1, 2 })
            {
                var input = await Fetch(consumer); var source = Decode(kind, input.Data!); var expected = FuturesPositionDeliveryFixture.DailyInput(kind, seq);
                Require(source.Id == expected.Id && source.CommandId == expected.CommandId && source.ReceivedOn == expected.ReceivedOn, "Position identity/time/order changed.");
                Require(await recovered.Deliver(source), "Unchanged-input position recovery failed.");
                Require(recovered.Requests.Count == 0 && recovered.Intents.Pending().Length == 0, "Recovery computed a later plan or left an intent pending.");
                await input.AckAsync(new AckOpts { DoubleAck = true });
            }
            await consumer.RefreshAsync(); Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Position sources remain pending.");
            await using var count = new NpgsqlCommand("SELECT count(*),min(revision),max(revision) FROM plan_receipts WHERE scenario=@scenario", pg); count.Parameters.AddWithValue("scenario", name);
            await using (var rows = await count.ExecuteReaderAsync())
            { await rows.ReadAsync(); Require(rows.GetInt64(0) == 1 && rows.GetInt64(1) == 1 && rows.GetInt64(2) == 1, "Plan receipts duplicated or lost."); }
            await using (var exits = new NpgsqlCommand("SELECT revision FROM exit_receipts WHERE scenario=@scenario ORDER BY ordinal", pg))
            {
                exits.Parameters.AddWithValue("scenario", name); await using var reader = await exits.ExecuteReaderAsync(); var revisions = new List<long>();
                while (await reader.ReadAsync()) revisions.Add(reader.GetInt64(0));
                Require(revisions.SequenceEqual(new long[] { 1 }), "Exit handoffs duplicated or reordered.");
            }
            Console.WriteLine($"PASS {storage}/{kind}/{mode}: original intent/owner/command bytes; later observation does not replace authority; one plan and owner receipt; no pending source/intent; restart clock +1 day");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Actual Daily position/clock source creation, actor/scheduler, flushed intent store, startup recovery and downstream owner Execute/state/mapping. PostgreSQL substitutes financial plan commits and owner event persistence; actual production command actor/projection/financial close not exercised. Current recovery position supplied by fixture. NATS Memory/File input broker stays up across API-child exit with one source in flight. No multi-host, broker/power loss or legacy uncaptured intent reconciliation claim.");
    }
    static void Bind(FuturesPositionDeliveryFixture fixture, NpgsqlConnection pg, string scenario)
    {
        fixture.Accept = async command =>
        {
            var bytes = MessagePackSerializer.Serialize(command);
            var state = new TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State.FuturesExitPositionWorkflowCommandState();
            byte[]? priorBytes = null;
            await using (var read = new NpgsqlCommand("SELECT payload,started FROM exit_receipts WHERE scenario=@scenario AND command_id=@id", pg))
            {
                read.Parameters.AddWithValue("scenario", scenario); read.Parameters.AddWithValue("id", command.CommandId);
                await using var reader = await read.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                { priorBytes = reader.GetFieldValue<byte[]>(0); state.ReplayEvents(new IEvent[] { MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.ExitPositionWorkflowStartedEvent>(reader.GetFieldValue<byte[]>(1)) }); }
            }
            var result = command.Execute(state);
            Require(result.Success, "Actual owner rejected captured Daily authority: " + result.ErrorMessage);
            if (priorBytes is not null) { Require(priorBytes.AsSpan().SequenceEqual(bytes), "Changed exit bytes under same ID."); return; }
            await using var insert = new NpgsqlCommand("INSERT INTO exit_receipts(scenario,command_id,payload,started,revision) VALUES(@scenario,@id,@payload,@started,@revision)",pg);
            insert.Parameters.AddWithValue("scenario",scenario); insert.Parameters.AddWithValue("id",command.CommandId);
            insert.Parameters.AddWithValue("payload",bytes); insert.Parameters.AddWithValue("started",MessagePackSerializer.Serialize(state.Started!));
            insert.Parameters.AddWithValue("revision",command.ExitPlan.Plan.PlanRevision); await insert.ExecuteNonQueryAsync();
        };
        fixture.Commit = async (command, completed) =>
        {
            await using (var insert = new NpgsqlCommand("INSERT INTO plan_receipts(scenario,command_id,fingerprint,payload,revision) VALUES(@scenario,@id,@fingerprint,@payload,@revision) ON CONFLICT DO NOTHING", pg))
            {
                insert.Parameters.AddWithValue("scenario", scenario); insert.Parameters.AddWithValue("id", command.CommandId); insert.Parameters.AddWithValue("fingerprint", command.Fingerprint());
                insert.Parameters.AddWithValue("payload", MessagePackSerializer.Serialize(completed)); insert.Parameters.AddWithValue("revision", completed.Plan.PlanRevision); await insert.ExecuteNonQueryAsync();
            }
            await using var read = new NpgsqlCommand("SELECT payload FROM plan_receipts WHERE scenario=@scenario AND command_id=@id", pg); read.Parameters.AddWithValue("scenario", scenario); read.Parameters.AddWithValue("id", command.CommandId);
            var original = MessagePackSerializer.Deserialize<FuturesTradePlanUpdatedEvent>((byte[])(await read.ExecuteScalarAsync())!);
            Require(original.RequestFingerprint == command.Fingerprint(), "Same deterministic command changed its captured inputs."); return original;
        };
    }
    public static async Task CrashChild(string name, string kind, string mode)
    {
        var pg = new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_DAILY_EXIT_TEST_PG")); await pg.OpenAsync();
        var fixture = new FuturesPositionDeliveryFixture(false, intentDirectory: Environment.GetEnvironmentVariable("IFM_DAILY_EXIT_TEST_DIRECTORY")) { Failure = mode == "completed-no-ack" ? "none" : mode }; Bind(fixture, pg, name); await fixture.Start();
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_DAILY_EXIT_TEST_NATS")! }); var consumer = await new NatsJSContext(connection).GetConsumerAsync(name, "ordered");
        var input = await Fetch(consumer); var source = Decode(kind, input.Data!);
        if (mode == "before-owner-crash") fixture.BeforeExit = CrashAfterCapture;
        if (mode == "after-owner-crash") fixture.AfterExit = CrashAfterCapture;
        Require(await fixture.Deliver(source) == (mode == "completed-no-ack"), "Unexpected position child settlement.");
        var overtook = 0;
        await foreach (var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) if (Decode(kind, probe.Data!).Id != source.Id) overtook++;
        Require(overtook == 0, "Successor overtook the original position source.");
        Console.WriteLine("DAILY_EXIT_INPUT_RETAINED"); Environment.Exit(0); // No ACK or actor/connection disposal.
    }
    static void CrashAfterCapture() { Console.WriteLine("DAILY_EXIT_INPUT_RETAINED"); Environment.Exit(0); }
    static byte[] Encode(PositionChangedEvent source) => MessagePackSerializer.Serialize(source);
    static PositionChangedEvent Decode(string kind, byte[] bytes) => MessagePackSerializer.Deserialize<PositionChangedEvent>(bytes);
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected position input was not redelivered.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
