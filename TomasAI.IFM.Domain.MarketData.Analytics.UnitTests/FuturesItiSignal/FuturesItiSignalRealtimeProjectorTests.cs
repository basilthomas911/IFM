using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalRealtimeProjectorTests
{
    [Fact]
    public void Projector_covers_iti_and_temporary_trade_signal_without_durable_dependencies()
    {
        var projector = new FuturesItiSignalRealtimeProjector(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeProjector>>());

        projector.Should().BeAssignableTo<IRealtimeProjector<FuturesItiSignalRealtimeActor>>();
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().BeEquivalentTo([
                typeof(FuturesItiSignalGeneratedEvent),
                typeof(FuturesTradeSignalUpdatedEvent)]);

        var parameterTypes = typeof(FuturesItiSignalRealtimeProjector)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType);
        parameterTypes.Should().NotContain(typeof(IEventSourceActorDbContext));
        parameterTypes.Should().NotContain(typeof(IDurableReplayQueue));
    }
}
