using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream.Models;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionRecoveryNotificationIntegrationTests
{
    [Fact]
    public async Task Native_backend_owns_notifications_without_Csharp_polling_or_publication()
    {
        var market=Substitute.For<IMarketDataApi>();
        market.DatasetRecoveryOwnership.Returns(MarketDataRecoveryOwnership.TokioDatasetOwner);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(
            true,new DateOnly(2026,10,28),[],RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner));
        var producer=Substitute.For<IJSActorProducer>();
        using var service=new OptionFeedRecoveryNotificationService(market,producer,NullLogger<OptionFeedRecoveryNotificationService>.Instance);
        await service.StartAsync(default);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(market.ReceivedCalls(),call=>call.GetMethodInfo().Name==nameof(IMarketDataApi.GetBackendStatusAsync));
        Assert.Empty(producer.ReceivedCalls());
    }

    [IsolatedRecoveryBrokerTheory(Timeout = 20000)]
    [InlineData(true)]
    public async Task Failed_full_recovery_notifies_UI_even_when_Host_status_never_returns(bool uncooperative)
    {
        Assert.True(uncooperative);
        var url = Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL")!;
        var clock = new LivePipelineIntegrationTests.ManualTime();
        var runtime = new HungRuntime();
        var recovery = new TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionPipelineRecoveryCoordinator(runtime,
            NullLogger<TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionPipelineRecoveryCoordinator>.Instance, clock);
        recovery.Observe(CancellationToken.None);
        await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromMinutes(2)); await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(2));
        var failure = recovery.Failure!;
        await using var client = new NatsClient(url); await client.ConnectAsync(); var js = client.CreateJetStreamContext();
        var name = "OPTION_FAILED_RECOVERY_" + Guid.NewGuid().ToString("N");
        var stream = await js.CreateStreamAsync(new StreamConfig(name, [failure.Subject.ToString()]));
        try
        {
            var market = Substitute.For<IMarketDataApi>();
            market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<MarketDataBackendStatus>().Task);
            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            await using var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url = url }, NullLogger<NatsJetStreamActorProducer>.Instance);
            using var service = new OptionFeedRecoveryNotificationService(market, producer, NullLogger<OptionFeedRecoveryNotificationService>.Instance, recovery: recovery);
            await service.StartAsync(CancellationToken.None);
            try
            {
                var consumer = await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("ui")
                { FilterSubject = failure.Subject.ToString(), AckPolicy = ConsumerConfigAckPolicy.Explicit });
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var received = false;
                await foreach (var message in consumer.ConsumeAsync<byte[]>(serializer: new NatsByteArrayMessageSerializer(), cancellationToken: timeout.Token))
                {
                    var error = ActorExtensions.DataSerializer.Deserialize<MarketDataDatasetRecoveryFailedEvent>(message.Data!)!;
                    Assert.Equal(failure.CommandId, error.CommandId); Assert.Contains("deadline expired", error.ErrorMessage);
                    Assert.Contains("API restart is disabled", error.ErrorMessage);
                    await message.AckAsync(cancellationToken: timeout.Token); received = true; break;
                }
                Assert.True(received);
            }
            finally { await service.StopAsync(CancellationToken.None); }
        }
        finally { await js.DeleteStreamAsync(name); }
    }

    sealed class HungRuntime : TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.IOptionPipelineRecoveryRuntime
    {
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionPipelineInspection Inspect() => new(new(2026, 10, 22), "stalled", ["trade-a"]);
        public Task RecoverAndVerifyAsync(Guid id, TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionPipelineInspection inspection, DateTimeOffset deadline, CancellationToken token)
        { Entered.TrySetResult(); return new TaskCompletionSource().Task; }
    }

    [IsolatedRecoveryBrokerTheory(Timeout = 20000)]
    [InlineData(true)]
    public async Task Api_mailbox_deadline_reaches_durable_UI_channel_without_claiming_worker_cleanup(bool notificationOnly)
    {
        Assert.True(notificationOnly);
        var url = Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL")!;
        var clock = new OptionPipelineFailureObserverTests.Clock();
        var observer = new OptionPipelineFailureObserver(clock);
        observer.Observe(new(2026, 10, 22), [OptionPipelineFailureObserverTests.Mailbox()]);
        clock.Advance(119);
        Assert.Empty(observer.Observe(new(2026, 10, 22), [OptionPipelineFailureObserverTests.Mailbox()]));
        clock.Advance(1);
        var failed = Assert.Single(observer.Observe(new(2026, 10, 22), [OptionPipelineFailureObserverTests.Mailbox()]));
        await using var client = new NatsClient(url); await client.ConnectAsync();
        var js = client.CreateJetStreamContext();
        var name = "OPTION_PIPELINE_UI_" + Guid.NewGuid().ToString("N");
        var stream = await js.CreateStreamAsync(new StreamConfig(name, [failed.Subject.ToString()]));
        try
        {
            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            await using var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url = url }, NullLogger<NatsJetStreamActorProducer>.Instance);
            await producer.StartAsync(new(ActorType.Event, nameof(OptionPipelineFailureObserver)), CancellationToken.None);
            await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent, TomasAI.IFM.Domain.MarketData.Feed.Shared.MarketDataFeedId>(failed.Subject, failed);
            observer.MarkPublished(failed.CommandId);
            // Consumer attaches AFTER publication, like a UI reconnecting after the deadline.
            var consumer = await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("desktop")
                { FilterSubject = failed.Subject.ToString(), AckPolicy = ConsumerConfigAckPolicy.Explicit });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var received = false;
            await foreach (var message in consumer.ConsumeAsync<byte[]>(serializer: new NatsByteArrayMessageSerializer(), cancellationToken: timeout.Token))
            {
                var restored = ActorExtensions.DataSerializer.Deserialize<MarketDataDatasetRecoveryFailedEvent>(message.Data!)!;
                Assert.Equal(failed.CommandId, restored.CommandId);
                Assert.Equal("Options", restored.Role);
                Assert.Contains("120 seconds", restored.ErrorMessage);
                Assert.Contains("restart is disabled", restored.ErrorMessage);
                Assert.Equal(Guid.Empty, restored.WorkerInstanceId);
                await message.AckAsync(cancellationToken: timeout.Token); received = true; break;
            }
            Assert.True(received);
            await producer.StopAsync(CancellationToken.None);
        }
        finally { await js.DeleteStreamAsync(name); }
    }

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
            var logger = Substitute.For<ILogger<OptionFeedRecoveryNotificationService>>();
            using var service = new OptionFeedRecoveryNotificationService(market, producer, logger);
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
            Assert.Contains(logger.ReceivedCalls(),call => call.GetMethodInfo().Name == "Log"
                && call.GetArguments()[2]?.ToString() is { } entry
                && entry.Contains("Backend failure notification published",StringComparison.Ordinal)
                && entry.Contains(subject,StringComparison.Ordinal)
                && entry.Contains(status.OperationId.ToString(),StringComparison.Ordinal));
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
