using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.MarketOutlookSnapshot;

public sealed class MarketOutlookHotCachePipelineIntegrationTests
{
    [Fact]
    public void ComponentToProjectionToNotification_RoundTripsTheCommittedCacheValue()
    {
        var id = new MarketOutlookEntityId("ESZ26", new DateOnly(2026, 9, 1));
        var cache = new MarketOutlookHotCache();
        var current = cache.Write(id,
            [new(CacheComponentType.Vx, new(Guid.NewGuid(), 1, DateTime.UtcNow))],
            state => state with { VixFuturesPrice = 22.75m },
            state => MarketOutlookComposer.Compose(
                state, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow)).Snapshot;
        var notification = new MarketOutlookUpdatedNotifyEvent
        {
            Subject = new(ActorType.Notify, MarketOutlookUpdatedNotifyEvent.Actor,
                MarketOutlookUpdatedNotifyEvent.Verb, id.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = id,
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = current
        };

        var received = MessagePackSerializer.Deserialize<MarketOutlookUpdatedNotifyEvent>(
            MessagePackSerializer.Serialize(notification));

        cache.TryGetCurrent(id, out var queried).Should().BeTrue();
        received.MarketOutlook.Should().Be(queried);
        queried.VixFuturesPrice.Should().Be(22.75m);
    }

    [Fact]
    public void Cache_IsImmediatelyWritableWithoutFeedActivationAndOnlyExplicitClearRemovesState()
    {
        var id = new MarketOutlookEntityId("ESZ26", new DateOnly(2026, 9, 1));
        var cache = new MarketOutlookHotCache();
        cache.Write(id,
            [new(CacheComponentType.Vx, new(Guid.NewGuid(), 1, DateTime.UtcNow))],
            state => state with { VixFuturesPrice = 20m },
            state => MarketOutlookComposer.Compose(
                state, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow));

        cache.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(20m);

        cache.Clear();
        cache.TryGetInputs(id, out _).Should().BeFalse();
    }
}
