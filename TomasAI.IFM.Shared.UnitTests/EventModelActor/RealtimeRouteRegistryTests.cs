using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class RealtimeRouteRegistryTests
{
    static readonly ActorTypeId Source =
        new(ActorType.Realtime, "FuturesMarketPrice", "Updated");

    [Fact]
    public void PublishedSnapshot_RemainsStableAfterReplacementAndRemoval()
    {
        var registry = new RealtimeRouteRegistry();
        var destination = new ActorMailboxId(ActorType.Realtime, "FuturesItiSignal");
        var subject = new ActorSubject(ActorType.Realtime, "FuturesMarketPrice", "Updated", "ESZ26");
        registry.Add(Source, new RealtimeActorRoute(destination));
        var original = registry.GetSnapshot(Source);

        registry.GetSnapshot(Source).Equals(original).Should().BeTrue("unchanged routes reuse their snapshot");
        registry.Add(Source, new RealtimeActorRoute(destination, _ => "session"));
        var replaced = registry.GetSnapshot(Source);
        registry.Remove(Source, destination);

        original.Should().ContainSingle().Which.Resolve(subject).EntityId.Should().Be("ESZ26");
        replaced.Should().ContainSingle().Which.Resolve(subject).EntityId.Should().Be("session");
        registry.GetSnapshot(Source).Should().BeEmpty();
    }

    [Fact]
    public void ConcurrentRouteChanges_PreserveAllDestinationsAndExistingSnapshots()
    {
        var registry = new RealtimeRouteRegistry();
        var destinations = Enumerable.Range(0, 128)
            .Select(i => new ActorMailboxId(ActorType.Realtime, $"Destination{i}")).ToArray();
        Parallel.ForEach(destinations, destination => registry.Add(Source, new RealtimeActorRoute(destination)));
        var published = registry.GetSnapshot(Source);
        published.Select(route => route.Destination).Should().BeEquivalentTo(destinations);

        Parallel.For(0, destinations.Length, i =>
        {
            if (i % 2 == 0)
                registry.Remove(Source, destinations[i]);
            else
                registry.Add(Source, new RealtimeActorRoute(destinations[i], _ => "session"));
            var snapshot = registry.GetSnapshot(Source);
            snapshot.Select(route => route.Destination).Should().OnlyHaveUniqueItems();
        });

        published.Should().HaveCount(destinations.Length);
        registry.GetSnapshot(Source).Select(route => route.Destination)
            .Should().BeEquivalentTo(destinations.Where((_, i) => i % 2 != 0));
    }

    [Fact]
    public async Task Supervisor_DeduplicatesAndRemovesRealtimeRoutes()
    {
        await using var supervisor = CreateSupervisor();
        var destination =
            new ActorMailboxId(ActorType.Realtime, "FuturesItiSignalRealtime");

        supervisor.AddRealtimeRouter(Source, destination);
        supervisor.AddRealtimeRouter(Source, destination);

        supervisor.GetRealtimeRoutes(Source)
            .Should().ContainSingle().Which.Destination.Should().Be(destination);

        supervisor.RemoveRealtimeRouter(Source, destination);

        supervisor.GetRealtimeRoutes(Source).Should().BeEmpty();
    }

    [Fact]
    public async Task Supervisor_ReplacesDestinationRouteWithSchedulingEntityProjection()
    {
        await using var supervisor = CreateSupervisor();
        var destination = new ActorMailboxId(ActorType.Realtime, "FuturesTradeSessionBarSignal");
        var source = new ActorSubject(ActorType.Realtime, "FuturesMarketPrice", "Updated", "ESZ26");

        supervisor.AddRealtimeRouter(Source, destination);
        supervisor.AddRealtimeRouter(Source, destination, _ => "2026-09-02");

        var route = supervisor.GetRealtimeRoutes(Source).Should().ContainSingle().Which;
        route.Destination.Should().Be(destination);
        route.Resolve(source).Should().Be(new ActorSubject(
            ActorType.Realtime,
            destination.Name,
            source.Verb,
            "2026-09-02"));
    }

    [Theory]
    [InlineData(ActorType.Event, ActorType.Realtime)]
    [InlineData(ActorType.Realtime, ActorType.Event)]
    [InlineData(ActorType.Notify, ActorType.Notify)]
    public async Task Supervisor_RejectsNonRealtimeRouteEndpoints(
        ActorType sourceType,
        ActorType destinationType)
    {
        await using var supervisor = CreateSupervisor();
        var source = new ActorTypeId(sourceType, "Source", "Updated");
        var destination = new ActorMailboxId(destinationType, "Destination");

        var action = () => supervisor.AddRealtimeRouter(source, destination);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Realtime routes require*");
    }

    static ActorSupervisor CreateSupervisor()
        => new(
            new Mock<IContainerInstance>().Object,
            NullLogger<ActorSupervisor>.Instance);
}
