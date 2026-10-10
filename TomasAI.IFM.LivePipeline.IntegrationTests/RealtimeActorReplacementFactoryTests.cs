using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class RealtimeActorReplacementFactoryTests
{
    [Fact]
    public void ReplacementBypassesSingletonContextAndKeepsSharedServices()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var market = Substitute.For<IMarketDataApi>();
        var evidence = new LivePipelineEvidence(TimeProvider.System);
        var telemetry = new FuturesItiSignalRuntimeTelemetry(TimeProvider.System);
        var logger = Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>();
        var original = new FuturesItiSignalRealtimeContext(supervisor, market, evidence, telemetry, logger);
        var singletonActor = new FuturesItiSignalRealtimeActor(original);
        var dependencies = new Dictionary<Type, object>
        {
            [typeof(IRealtimeActorContext<FuturesItiSignalRealtimeActor>)] = original,
            [typeof(IActorSupervisor)] = supervisor,
            [typeof(IMarketDataApi)] = market,
            [typeof(LivePipelineEvidence)] = evidence,
            [typeof(FuturesItiSignalRuntimeTelemetry)] = telemetry,
            [typeof(ILogger<FuturesItiSignalRealtimeActor>)] = logger
        };
        var replacement = (FuturesItiSignalRealtimeActor)RealtimeActorReplacementFactory.Create(
            typeof(FuturesItiSignalRealtimeActor), type => dependencies[type]);
        Assert.NotSame(singletonActor, replacement);
        Assert.NotSame(original.RealtimeGeneration, replacement.RealtimeGeneration);
        singletonActor.RetireRealtimeGeneration();
        Assert.True(original.RealtimeGeneration.Token.IsCancellationRequested);
        Assert.False(replacement.RealtimeGeneration.Token.IsCancellationRequested);
        using var execution = replacement.RealtimeGeneration.Enter();
        RealtimeActorGeneration.ThrowIfRetired();
    }
}
