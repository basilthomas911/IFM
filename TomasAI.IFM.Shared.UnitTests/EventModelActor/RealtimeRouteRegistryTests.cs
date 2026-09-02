using System;
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
