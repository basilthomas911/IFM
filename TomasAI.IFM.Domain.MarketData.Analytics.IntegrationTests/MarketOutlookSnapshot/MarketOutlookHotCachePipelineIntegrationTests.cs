using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        cache.Activate(new(id.ContractId, id.ValueDate, Guid.NewGuid()));
        cache.TryUpdateInput(id, CacheComponentType.Vx,
            new(Guid.NewGuid(), 1, DateTime.UtcNow),
            state => state with { VixFuturesPrice = 22.75m }, out var inputs).Should().BeTrue();
        var current = MarketOutlookComposer.Compose(
            inputs, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow);
        cache.SetCurrent(current);
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
    public async Task ApiHostedWorker_ActivatesGenerationFenceAndClearsDerivedStateOnStop()
    {
        var fence = new MarketOutlookGenerationFence(
            "ESZ26", new DateOnly(2026, 9, 1), Guid.NewGuid());
        var cache = new MarketOutlookHotCache();
        var authority = Substitute.For<IMarketDataGenerationAuthority>();
        authority.TryGetActive(out Arg.Any<MarketOutlookGenerationFence>())
            .Returns(call =>
            {
                call[0] = fence;
                return true;
            });
        var worker = new MarketOutlookHotCacheService(
            cache,
            authority,
            Substitute.For<ILogger<MarketOutlookHotCacheService>>());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        cache.ActiveFence.Should().Be(fence);
        cache.TryUpdateInput(new(fence.ContractId, fence.ValueDate), CacheComponentType.Vx,
            new(Guid.NewGuid(), 1, DateTime.UtcNow),
            state => state with { VixFuturesPrice = 20m }, out _).Should().BeTrue();

        await worker.StopAsync(CancellationToken.None);

        cache.ActiveFence.IsValid.Should().BeFalse();
        cache.TryGetInputs(new(fence.ContractId, fence.ValueDate), out _).Should().BeFalse();
    }
}
