using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.JetStream.Models;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionRecoveryNotificationIntegrationTests
{
    [IsolatedRecoveryBrokerTheory(Timeout = 20000)]
    [InlineData("Options")]
    [InlineData("Futures")]
    public async Task Terminal_recovery_error_is_persisted_and_delivered_over_JetStream(string role)
    {
        // This test deliberately requires a separate broker; domain event subjects must never reach a running dev UI.
        var url = Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL");
        Assert.False(string.IsNullOrWhiteSpace(url), "Set IFM_RECOVERY_TEST_NATS_URL to an isolated JetStream broker.");
        await using var client = new NatsClient(url!);
        await client.ConnectAsync();
        var js = client.CreateJetStreamContext();
        var streamName = "RECOVERY_UI_" + Guid.NewGuid().ToString("N");
        var status = new MarketDataDatasetStatus(role, "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 123,
            false, false, "StoppedError", "Recovery deadline of 120.000s expired during option lease restoration.",
            new(2026, 10, 22), Guid.NewGuid());
        var subject = role == "Options" ? OptionFeedRecoveryNotificationService.CreateEvent(status).Subject.ToString()
            : OptionFeedRecoveryNotificationService.CreateDatasetEvent(status).Subject.ToString();
        var stream = await js.CreateStreamAsync(new StreamConfig(streamName, [subject]));
        try
        {
            var consumer = await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("ui")
            { FilterSubject = subject, AckPolicy = ConsumerConfigAckPolicy.Explicit });
            var market = Substitute.For<IMarketDataApi>();
            market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, new(2026, 10, 22), [status]));
            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            await using var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url = url! }, NullLogger<NatsJetStreamActorProducer>.Instance);
            using var service = new OptionFeedRecoveryNotificationService(market, producer, NullLogger<OptionFeedRecoveryNotificationService>.Instance);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.StartAsync(timeout.Token);
            try
            {
                await foreach (var message in consumer.ConsumeAsync<byte[]>(serializer: new NatsByteArrayMessageSerializer(), cancellationToken: timeout.Token))
                {
                    Assert.NotNull(message.Data);
                    // Actor messages use their existing serializer/envelope, exactly as the UI listener receives them.
                    var error = role == "Options"
                        ? ActorExtensions.DataSerializer.Deserialize<OptionMarketDataFeedFailedEvent>(message.Data)!.ErrorMessage
                        : ActorExtensions.DataSerializer.Deserialize<MarketDataDatasetRecoveryFailedEvent>(message.Data)!.ErrorMessage;
                    Assert.Contains("120.000s", error);
                    Assert.Contains("option lease restoration", error);
                    await message.AckAsync(cancellationToken: timeout.Token);
                    break;
                }
                Assert.True((await js.GetStreamAsync(streamName)).Info.State.Messages >= 1);
            }
            finally { await service.StopAsync(CancellationToken.None); }
            Assert.False(producer.IsRunning);
        }
        finally { await js.DeleteStreamAsync(streamName); }
    }
}

/// <summary>Requires a separate broker because these tests use real desktop event subjects.</summary>
public sealed class IsolatedRecoveryBrokerTheoryAttribute : TheoryAttribute
{
    public IsolatedRecoveryBrokerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL")))
            Skip = "Set IFM_RECOVERY_TEST_NATS_URL to an isolated JetStream broker.";
    }
}
