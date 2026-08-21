using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.EventConsumers;

public sealed class FuturesItiSignalUIEventConsumerTests
{
    [Fact]
    public void Dispatch_ForwardsEveryModeAndIsolatesSubscribers()
    {
        var consumer = new FuturesItiSignalUIEventConsumer(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger>());
        var failingSite = Guid.NewGuid();
        var recordingSite = Guid.NewGuid();
        var received = new List<IntrinsicTimeModeType>();

        consumer.AddSubscriber(failingSite, _ => throw new InvalidOperationException("view failed"))
            .Should().BeTrue();
        consumer.AddSubscriber(recordingSite, notification =>
                received.Add(notification.FuturesItiSignal.IntrinsicTimeMode))
            .Should().BeFalse();

        foreach (var mode in Enum.GetValues<IntrinsicTimeModeType>())
            consumer.Dispatch(Notification(mode));

        received.Should().Equal(Enum.GetValues<IntrinsicTimeModeType>());
        consumer.RemoveSubscriber(failingSite).Should().BeFalse();
        consumer.RemoveSubscriber(recordingSite).Should().BeTrue();
    }

    [Fact]
    public void Dispatch_IgnoresInvalidNotificationWithoutInvokingSubscriber()
    {
        var consumer = new FuturesItiSignalUIEventConsumer(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger>());
        var invoked = false;
        consumer.AddSubscriber(Guid.NewGuid(), _ => invoked = true);

        consumer.Dispatch(Notification(IntrinsicTimeModeType.Trending) with
        {
            CommandId = Guid.Empty
        });

        invoked.Should().BeFalse();
    }

    static FuturesItiSignalUpdatedNotifyEvent Notification(IntrinsicTimeModeType mode)
    {
        var signal = new FuturesItiSignalV2ReadModel
        {
            ContractId = "ESZ26",
            ValueDate = new DateOnly(2026, 8, 21),
            TimePeriod = TimeFrameType.Daily,
            SequenceId = (int)mode + 1,
            IntrinsicTime = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Utc)
                .AddSeconds((int)mode),
            IntrinsicTimeMode = mode,
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend
        };
        return new FuturesItiSignalUpdatedNotifyEvent
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                FuturesItiSignalUpdatedNotifyEvent.Actor,
                FuturesItiSignalUpdatedNotifyEvent.Verb,
                signal.EntityId.Format()),
            Id = Guid.NewGuid(),
            SourceEventId = Guid.NewGuid(),
            EntityId = signal.EntityId,
            CommandId = Guid.NewGuid(),
            ReceivedOn = signal.IntrinsicTime,
            FuturesItiSignal = signal
        };
    }
}
