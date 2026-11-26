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

public static class IronCondorPlanPublicationVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("condorpublication", includeCql: false); await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using var pg = new NpgsqlConnection(cs); await pg.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE plan_receipts(scenario text, command_id uuid, fingerprint text NOT NULL, payload bytea NOT NULL, revision bigint NOT NULL, PRIMARY KEY(scenario,command_id)); CREATE TABLE notifications(ordinal bigserial, scenario text NOT NULL, id uuid NOT NULL, payload bytea NOT NULL, sequence bigint NOT NULL, PRIMARY KEY(scenario,id));", pg)) await create.ExecuteNonQueryAsync();
        var capturesRoot = Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety", "condor-publication-captures-" + Guid.NewGuid().ToString("N"));
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var kind in new[] { "Position", "Monitoring" })
        foreach (var mode in new[] { "before-send", "after-send", "completed-no-ack", "before-send-crash", "after-send-crash" })
        {
            var name = $"CONDOR_PUBLICATION_{storage}_{kind}_{mode.Replace('-', '_')}"; var subject = "condorcase." + name;
            await using var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }); var js = new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name, [subject]) { Storage = storage, Retention = StreamConfigRetention.Workqueue, MaxMsgs = 16, MaxBytes = 1024 * 1024, Discard = StreamConfigDiscard.New });
            await js.CreateOrUpdateConsumerAsync(name, new ConsumerConfig("ordered") { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
            await using (var inputs = new IronCondorPositionDeliveryFixture(false))
                foreach (var seq in new[] { 1, 2 }) (await js.PublishAsync(subject, Encode(inputs.Input(kind, seq)), serializer: Serializer)).EnsureSuccess();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(IronCondorPlanPublicationVerification).Assembly.Location);
            foreach (var arg in new[] { "--condor-publication-crash-child", name, kind, mode }) start.ArgumentList.Add(arg);
            start.Environment["IFM_CONDOR_TEST_CAPTURE"] = Path.Combine(capturesRoot, name);
            start.Environment["IFM_CONDOR_TEST_NATS"] = infrastructure.NatsUrl; start.Environment["IFM_CONDOR_TEST_PG"] = cs;
            using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); } catch { child.Kill(entireProcessTree: true); throw; }
            Require(child.ExitCode == 0 && (await stdout).Contains("CONDOR_INPUT_RETAINED"), "Condor child failed: " + await stderr);
            await using var recovered = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: Path.Combine(capturesRoot, name), realPublication: true);
            recovered.Time.Now += TimeSpan.FromDays(1); recovered.Inputs = recovered.Inputs with { PutForwardPrice = 99 }; recovered.Parameters = new() { Version = 2, MaximumLoss = 2000 };
            Bind(recovered, pg, name); await recovered.Start();
            var consumer = await js.GetConsumerAsync(name, "ordered");
            foreach (var seq in new[] { 1, 2 })
            {
                var input = await Fetch(consumer); var source = Decode(kind, input.Data!); var expected = recovered.Input(kind, seq);
                Require(source.Id == expected.Id && source.CommandId == expected.CommandId && source.ReceivedOn == expected.ReceivedOn, "Position identity/time/order changed.");
                Require(await recovered.Deliver(source), "Frozen-request position recovery failed after time/cache/policy drift.");
                if (seq == 1)
                {
                    var request = recovered.PlanRequests.Single();
                    Require(request.RequestedAtUtc == IronCondorPositionDeliveryFixture.At && request.Parameters.Version == 1
                        && request.IronCondorTradePlanInputs!.PutForwardPrice == 5, "Original request time/inputs/policy were recaptured.");
                    Require(recovered.ForwardRequests.Count == 0, "Frozen plan repeated forward calculation.");
                }
                Require(recovered.Requests.Publications.Pending().Length == 0, "Recovered publication remains pending.");
                await input.AckAsync(new AckOpts { DoubleAck = true });
            }
            await consumer.RefreshAsync(); Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Position sources remain pending.");
            await using var count = new NpgsqlCommand("SELECT count(*),min(revision),max(revision) FROM plan_receipts WHERE scenario=@scenario", pg); count.Parameters.AddWithValue("scenario", name);
            await using (var rows = await count.ExecuteReaderAsync())
            { await rows.ReadAsync(); Require(rows.GetInt64(0) == 2 && rows.GetInt64(1) == 1 && rows.GetInt64(2) == 2, "Plan receipts duplicated or lost."); }
            await using (var readNotifications = new NpgsqlCommand("SELECT sequence FROM notifications WHERE scenario=@scenario ORDER BY ordinal", pg))
            {
                readNotifications.Parameters.AddWithValue("scenario", name);
                await using var reader = await readNotifications.ExecuteReaderAsync(); var order = new List<long>();
                while (await reader.ReadAsync()) order.Add(reader.GetInt64(0));
                Require(order.SequenceEqual(kind == "Position" ? new long[] { 1, 1, 2, 2 } : new long[] { 1, 2 }), "Notifications duplicated or reordered.");
            }
            Console.WriteLine($"PASS {storage}/{kind}/{mode}: exact original notification IDs/payloads; ordered sources and accepted notifications; two PostgreSQL surrogate plan receipts; no pending publication or source");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Actual actor/scheduler, request journal, production publication context/cache, binary outbox and startup recovery. Function calculation/commit and producer acceptance substituted with PostgreSQL receipt/notification ledgers. Memory/File JetStream retains ordered source; NATS stays running across API-child exit. Exact bytes deduplicate ambiguous sends. No live UI, broker-loss, production financial commit, actual generation authority or subscriber delivery guarantee is claimed.");
    }
    static void Bind(IronCondorPositionDeliveryFixture fixture, NpgsqlConnection pg, string scenario)
    {
        fixture.Publish = async notification =>
        {
            var bytes = MessagePackSerializer.Serialize(notification);
            await using (var insert = new NpgsqlCommand("INSERT INTO notifications(scenario,id,payload,sequence) VALUES(@scenario,@id,@payload,@sequence) ON CONFLICT DO NOTHING", pg))
            {
                insert.Parameters.AddWithValue("scenario", scenario); insert.Parameters.AddWithValue("id", notification.Id);
                insert.Parameters.AddWithValue("payload", bytes); insert.Parameters.AddWithValue("sequence", notification.Snapshot.Position!.PositionSequence); await insert.ExecuteNonQueryAsync();
            }
            await using var read = new NpgsqlCommand("SELECT payload FROM notifications WHERE scenario=@scenario AND id=@id", pg);
            read.Parameters.AddWithValue("scenario", scenario); read.Parameters.AddWithValue("id", notification.Id);
            Require(((byte[])(await read.ExecuteScalarAsync())!).AsSpan().SequenceEqual(bytes), "Notification identity reused with changed bytes.");
        };
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
        var fixture = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: Environment.GetEnvironmentVariable("IFM_CONDOR_TEST_CAPTURE"), realPublication: true)
        { FailBeforePublication = mode == "before-send", FailAfterPublication = mode == "after-send", PublicationPlanOnly = true };
        Bind(fixture, pg, name); await fixture.Start();
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_CONDOR_TEST_NATS")! }); var consumer = await new NatsJSContext(connection).GetConsumerAsync(name, "ordered");
        var input = await Fetch(consumer); var source = Decode(kind, input.Data!);
        if (mode == "before-send-crash") fixture.BeforePublication = CrashAfterCapture;
        if (mode == "after-send-crash") fixture.AfterPublication = CrashAfterCapture;
        Require(await fixture.Deliver(source) == (mode == "completed-no-ack"), "Unexpected position child settlement.");
        var overtook = 0;
        await foreach (var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) if (Decode(kind, probe.Data!).Id != source.Id) overtook++;
        Require(overtook == 0, "Successor overtook the original position source.");
        Console.WriteLine("CONDOR_INPUT_RETAINED"); Environment.Exit(0); // No ACK or actor/connection disposal.
    }
    static void CrashAfterCapture()
    {
        Console.WriteLine("CONDOR_INPUT_RETAINED"); Environment.Exit(0);
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
