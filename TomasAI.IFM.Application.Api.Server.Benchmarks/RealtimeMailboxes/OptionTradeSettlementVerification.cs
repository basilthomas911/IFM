using System.Diagnostics;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class OptionTradeSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    public static async Task Verify()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("optionsettlement", includeCql: false); await infrastructure.StartAsync();
        foreach (var storage in new[] { StreamConfigStorage.Memory, StreamConfigStorage.File })
        foreach (var metrics in new[] { false, true })
        foreach (var failure in new[] { "lookup", "send" })
        {
            var name = $"OPTION_{storage}_{metrics}_{failure}"; var subject = "option." + name;
            await using var connection = new NatsConnection(new NatsOpts { Url = infrastructure.NatsUrl }); var js = new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name, [subject]) { Storage = storage, Retention = StreamConfigRetention.Workqueue, MaxMsgs = 16, MaxBytes = 1024 * 1024, Discard = StreamConfigDiscard.New });
            await js.CreateOrUpdateConsumerAsync(name, new ConsumerConfig("ordered") { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, AckWait = TimeSpan.FromSeconds(3), MaxDeliver = -1 });
            foreach (var seq in new[] { 1, 2 }) (await js.PublishAsync(subject, MessagePackBinarySerializer.Shared.Serialize(OptionTradeDeliveryFixture.Input(seq))!, serializer: Serializer)).EnsureSuccess();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(OptionTradeSettlementVerification).Assembly.Location);
            foreach (var arg in new[] { "--option-settlement-crash-child", name, metrics.ToString(), failure }) start.ArgumentList.Add(arg);
            start.Environment["IFM_OPTION_TEST_NATS"] = infrastructure.NatsUrl;
            using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); } catch { child.Kill(entireProcessTree: true); throw; }
            Require(child.ExitCode == 0 && (await stdout).Contains("OPTION_INPUT_RETAINED"), "Option child failed: " + await stderr);
            await using var fixture = new OptionTradeDeliveryFixture(metrics); await fixture.Start(); var consumer = await js.GetConsumerAsync(name, "ordered");
            foreach (var seq in new[] { 1, 2 })
            {
                var input = await Fetch(consumer); var source = MessagePackBinarySerializer.Shared.Deserialize<FuturesTickTradeDataInsertedEvent>(input.Data!)!;
                var expected = OptionTradeDeliveryFixture.Input(seq);
                Require(source.Id == expected.Id && source.CommandId == expected.CommandId && source.ReceivedOn == expected.ReceivedOn && source.TickDataId == expected.TickDataId, "Source identity/timestamp/order changed on redelivery.");
                Require(await fixture.Deliver(source), "Recovered option delivery failed.");
                Require(fixture.Accepted.Count == seq && fixture.Accepted[^1].OptionTickData.TickId == seq && fixture.Accepted[^1].CommandId == source.CommandId, "Notification sequence/causation changed.");
                await input.AckAsync(new AckOpts { DoubleAck = true });
            }
            await consumer.RefreshAsync(); Require(consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0, "Option input remains pending.");
            Require(fixture.Accepted.All(e => e.OptionTickData.OptionPrice == 13.125 && e.OptionTickData.BidPrice == 12.25 && e.OptionTickData.AskPrice == 12.75), "Replayed trade/quote values changed.");
            Console.WriteLine($"PASS {storage}/metrics={metrics}/{failure}: failed source retained, successor blocked, original identity/time/order recovered, two accepted notifications");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Actual option actor/scheduler/settlement. Cache/catalog and Notify transport substituted. Failures occur before notification acceptance. Ordering uses one broker input in flight. No durable Notify deduplication, production JetStream adapter, broker-loss or financial persistence guarantee is claimed; NATS remains running during abrupt child exit.");
    }
    public static async Task CrashChild(string name, bool metrics, string failure)
    {
        var connection = new NatsConnection(new NatsOpts { Url = Environment.GetEnvironmentVariable("IFM_OPTION_TEST_NATS")! });
        var consumer = await new NatsJSContext(connection).GetConsumerAsync(name, "ordered");
        var fixture = new OptionTradeDeliveryFixture(metrics) { FailLookup = failure == "lookup", FailSend = failure == "send" }; await fixture.Start();
        var input = await Fetch(consumer); var source = MessagePackBinarySerializer.Shared.Deserialize<FuturesTickTradeDataInsertedEvent>(input.Data!)!;
        Require(!await fixture.Deliver(source) && fixture.Accepted.Count == 0, "Failed option delivery was incorrectly accepted.");
        var overtook = 0;
        await foreach (var _ in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer)) overtook++;
        Require(overtook == 0, "Successor overtook failed option input.");
        Console.WriteLine("OPTION_INPUT_RETAINED"); Environment.Exit(0); // Neither source ACK nor actor/connection disposal.
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {
        await foreach (var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) }, Serializer)) return input;
        throw new TimeoutException("Expected option source was not redelivered.");
    }
    static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
}
