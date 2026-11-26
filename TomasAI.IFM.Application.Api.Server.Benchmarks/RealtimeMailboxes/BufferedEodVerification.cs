using System.Diagnostics;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class BufferedEodVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("bufferedeod", includeCql: false); await infrastructure.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        await using var pg = new NpgsqlConnection(cs); await pg.OpenAsync();
        await using (var schema = new NpgsqlCommand(MarketDataSchemaDb.CreateStatement, pg)) await schema.ExecuteNonQueryAsync();
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var mode in new[] { "captured", "source", "database", "completion" })
        {
            var name = "BEOD_" + storage + "_" + mode; var directory = Path.Combine(Path.GetTempPath(), "IFM-buffered-crash-" + Guid.NewGuid().ToString("N"));
            await using var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }); var js = new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name, ["beod." + name + ".>"]) { Storage = storage, MaxMsgs = 32, MaxBytes = 1024 * 1024, Discard = StreamConfigDiscard.New });
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(BufferedEodVerification).Assembly.Location);
            foreach (var arg in new[] { "--buffered-eod-crash-child", name, mode, directory }) start.ArgumentList.Add(arg);
            start.Environment["IFM_BUFFERED_EOD_PG"] = cs; start.Environment["IFM_BUFFERED_EOD_NATS"] = infrastructure.NatsUrl;
            using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); } catch { child.Kill(entireProcessTree: true); throw; }
            Require(child.ExitCode == 0 && (await stdout).Contains("CAPTURE_OWNED"), "Buffered child failed: " + await stderr);
            Guid[] completionIds;
            using (var frozen = new EodCaptureJournal(directory)) completionIds = frozen.Batch(512).Select(item => item.Completion.Id).ToArray();
            await using (var fixture = Bind(directory, name, "recover", cs, js))
            {
                await fixture.Start(); await fixture.Projector.StopAsync();
                Require(fixture.Errors.Count == 0, "Recovery failed: " + string.Join("; ", fixture.Errors.Select(e => e.GetType().Name + ": " + e.Message)));
            }
            await using (var repeated = Bind(directory, name, "recover", cs, js))
            {
                await repeated.Start(); await repeated.Projector.ProcessRealtimeEventAsync(BufferedEodFixture.Source(1, name));
                await repeated.Projector.ProcessRealtimeEventAsync(BufferedEodFixture.Source(2, name)); await repeated.Projector.StopAsync();
                Require(repeated.Sent.Count == 0, "Completed input produced another publication.");
            }
            var consumer = await js.CreateOrUpdateConsumerAsync(name, new ConsumerConfig("verify") { AckPolicy = ConsumerConfigAckPolicy.Explicit });
            var sources = new List<Guid>(); var completions = new List<Guid>();
            await foreach (var message in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 5, Expires = TimeSpan.FromSeconds(1) }, Serializer))
            {
                if (message.Subject.EndsWith(".source", StringComparison.Ordinal)) sources.Add(MessagePackSerializer.Deserialize<FuturesEodDataInsertedEvent>(message.Data!).Id);
                else completions.Add(MessagePackSerializer.Deserialize<FuturesEodDataInsertedCompleteEvent>(message.Data!).Id);
                await message.AckAsync(new AckOpts { DoubleAck = true });
            }
            Console.WriteLine($"DEBUG {name} source={string.Join(",",sources)} completion={string.Join(",",completions)} expected={string.Join(",",completionIds)}");
            Require(sources.SequenceEqual(new[] { BufferedEodFixture.Source(1).Id, BufferedEodFixture.Source(2).Id }) && completions.SequenceEqual(completionIds), "Original source/completion identity or order changed.");
            await using var count = new NpgsqlCommand("SELECT count(*) FROM market_data.futures_intra_day_data WHERE contractid=@contract", pg); count.Parameters.AddWithValue("contract", name);
            Require(Convert.ToInt32(await count.ExecuteScalarAsync()) == 2, "History was lost or duplicated.");
            await using var current = new NpgsqlCommand("SELECT closeprice FROM market_data.futures_eod_data WHERE contractid=@contract", pg); current.Parameters.AddWithValue("contract", name);
            Require(Convert.ToDecimal(await current.ExecuteScalarAsync()) == 102, "Current EOD was reordered.");
            using (var journal = new EodCaptureJournal(directory)) Require(journal.Pending == 0, "Unfinished EOD capture remains.");
            Console.WriteLine($"PASS {storage}/{mode}: original IDs/order, two native PostgreSQL history rows, latest close 102, no pending receipt");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Production buffered projector, binary journal and MarketDataDb schema/mutations exercised. Confirmed/deduplicated JetStream publication is a test transport substitute; production Core NATS delivery and broker-loss recovery are not qualified. Broker remains running during abrupt child exit.");
    }
    static BufferedEodFixture Bind(string directory, string name, string mode, string cs, NatsJSContext js)
    {
        var fixture = new BufferedEodFixture(directory);
        var baseConnection = new NpgsqlConnectionStringBuilder(cs); baseConnection.Remove("Username"); baseConnection.Remove("Password");
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, baseConnection.ConnectionString, "System.Data.Postgres");
        var factory = Substitute.For<IDbContextFactory>(); var market = new MarketDataDbContext(settings, factory, fixture.Sequences, NullLogger<DbProvider>.Instance); factory.MarketDataDb.Returns(market);
        fixture.Persist = async rows => { try { await market.PersistRealtimeFuturesEodBatchAsync(rows); } catch (Exception error) { Console.WriteLine("DATABASE_FAILURE " + error.GetType().Name + ": " + error.Message); throw; } if (mode == "database") throw new IOException("Injected response loss after real database commit."); };
        fixture.Send = async input =>
        {
            var isSource = input is FuturesEodDataInsertedEvent;
            var headers = new NatsHeaders { ["Nats-Msg-Id"] = input.GetType().Name + ":" + input.Id.ToString("N") };
            var acknowledgement = await js.PublishAsync("beod." + name + (isSource ? ".source" : ".completion"), MessagePackSerializer.Serialize(input.GetType(), input), headers: headers, serializer: Serializer);
            if (!acknowledgement.Duplicate) acknowledgement.EnsureSuccess(); // A broker-confirmed duplicate is a successful retry.
            if (mode == (isSource ? "source" : "completion")) throw new IOException("Injected lost publication response.");
        };
        return fixture;
    }
    public static async Task CrashChild(string name, string mode, string directory)
    {
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_BUFFERED_EOD_NATS")! });
        var fixture = Bind(directory, name, mode, Environment.GetEnvironmentVariable("IFM_BUFFERED_EOD_PG")!, new NatsJSContext(connection));
        await fixture.Start(); await fixture.Projector.ProcessRealtimeEventAsync(BufferedEodFixture.Source(1, name)); await fixture.Projector.ProcessRealtimeEventAsync(BufferedEodFixture.Source(2, name));
        if (mode != "captured") await fixture.Projector.StopAsync();
        Console.WriteLine("CAPTURE_OWNED"); Environment.Exit(0); // No source redelivery is needed after durable capture.
    }
    static void Require(bool result, string message) { if (!result) throw new InvalidDataException(message); }
}
