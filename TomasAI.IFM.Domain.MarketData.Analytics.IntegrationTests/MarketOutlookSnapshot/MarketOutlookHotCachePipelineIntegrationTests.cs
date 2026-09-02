using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.MarketOutlookSnapshot;

public sealed class MarketOutlookHotCachePipelineIntegrationTests
{
    [Fact]
    public async Task ComponentToChannelToProjectionToNotification_RoundTripsCommittedValue()
    {
        var id = new MarketOutlookEntityId("ESZ26", new DateOnly(2026, 9, 1));
        await using var runtime = await Runtime.StartAsync();
        var now = DateTime.UtcNow;

        runtime.Channel.Submit(new HydrateMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = id,
            ReceivedAtUtc = now,
            MarketDataAsOfUtc = now,
            Baseline = new MarketOutlookInputState
            {
                EntityId = id,
                VixFuturesSessionOpenPrice = 20m,
                VixFuturesPrice = 19m,
                MarketDataAsOfUtc = now
            }
        });
        (await runtime.Processor.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        runtime.Channel.Submit(new VixPriceMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = id,
            ReceivedAtUtc = now,
            MarketDataAsOfUtc = now,
            Price = 22.75m,
            CommandId = Guid.NewGuid(),
            EventSource = "integration-test"
        });

        (await runtime.Processor.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        runtime.Publisher.Notification.Should().NotBeNull();
        var received = MessagePackSerializer.Deserialize<MarketOutlookUpdatedNotifyEvent>(
            MessagePackSerializer.Serialize(runtime.Publisher.Notification!));

        runtime.Cache.TryGetCurrent(id, out var queried).Should().BeTrue();
        received.MarketOutlook.Should().Be(queried);
        queried.VixFuturesPrice.Should().Be(22.75m);
        queried.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Rising);
        runtime.Processor.GetMetrics().Updates[MarketOutlookUpdateKind.VixPrice].Published.Should().Be(1);
    }

    [Fact]
    public async Task Channel_IsImmediatelyWritableWithoutFeedActivation_AndOnlyClearRemovesState()
    {
        var id = new MarketOutlookEntityId("ESZ26", new DateOnly(2026, 9, 1));
        await using var runtime = await Runtime.StartAsync();
        var now = DateTime.UtcNow;
        runtime.Channel.Submit(new VixPriceMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = id,
            ReceivedAtUtc = now,
            MarketDataAsOfUtc = now,
            Price = 20m
        });

        (await runtime.Processor.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        runtime.Cache.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(20m);

        runtime.Cache.Clear();
        runtime.Cache.TryGetInputs(id, out _).Should().BeFalse();
    }

    sealed class CapturingPublisher : IMarketOutlookSnapshotPublisher
    {
        public MarketOutlookUpdatedNotifyEvent? Notification { get; private set; }

        public ValueTask PublishAsync(
            MarketOutlookUpdate update,
            MarketOutlookReadModel snapshot,
            CancellationToken cancellationToken)
        {
            Notification = new()
            {
                Subject = new(ActorType.Notify, MarketOutlookUpdatedNotifyEvent.Actor,
                    MarketOutlookUpdatedNotifyEvent.Verb, update.EntityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = update.EntityId,
                CommandId = update.CommandId,
                EventSource = update.EventSource,
                ReceivedOn = DateTime.UtcNow,
                MarketOutlook = snapshot
            };
            return ValueTask.CompletedTask;
        }
    }

    sealed class Runtime : IAsyncDisposable
    {
        Runtime()
        {
            Cache = new();
            var metrics = new MarketOutlookProcessorMetrics();
            Channel = new(metrics);
            Publisher = new();
            Processor = new(
                Channel, Channel, Cache, Cache, Publisher, metrics,
                Substitute.For<ILogger<MarketOutlookUpdateProcessor>>());
        }

        public MarketOutlookHotCache Cache { get; }
        public MarketOutlookUpdateChannel Channel { get; }
        public CapturingPublisher Publisher { get; }
        public MarketOutlookUpdateProcessor Processor { get; }

        public static async ValueTask<Runtime> StartAsync()
        {
            var runtime = new Runtime();
            await runtime.Processor.StartAsync(CancellationToken.None);
            return runtime;
        }

        public async ValueTask DisposeAsync()
        {
            await Processor.StopAsync(CancellationToken.None);
            Processor.Dispose();
        }
    }
}
