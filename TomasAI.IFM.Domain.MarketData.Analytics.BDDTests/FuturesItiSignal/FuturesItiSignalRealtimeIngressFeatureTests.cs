using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesItiSignal;

/// <summary>Defines executable ingress behavior from a normalized ES trade to Daily ITI evaluation.</summary>
public sealed class FuturesItiSignalRealtimeIngressFeatureTests
{
    const string Es = "ES20260918";
    const string Vx = "VX20260916";
    static readonly DateOnly Date = new(2026, 9, 14);

    [Fact]
    public async Task GivenCurrentEsAndVx_WhenAnEsTradeArrives_ThenOneDailyEvaluationIsRequested()
    {
        var context = Context(vxAvailable: true, out _);
        GenerateFuturesItiSignalCommand? requested = null;
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Do<GenerateFuturesItiSignalCommand>(command => requested = command))
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));

        var handled = await Event().ExecuteAsync(context);
        await context.GenerationGate.WaitForIdleAsync();

        handled.Should().BeTrue();
        requested.Should().NotBeNull();
        requested!.TimePeriod.Should().Be(TimeFrameType.Daily);
        requested.EntityId.ContractId.Should().Be(Es);
        requested.FuturesPrice.Should().Be(5450.25);
        requested.VixFuturesPrice.Should().Be(22.75);
    }

    [Fact]
    public async Task GivenVxIsUnavailable_WhenEsTradesContinue_ThenIngressDegradesWithoutThrowingOrRetryLoop()
    {
        var context = Context(vxAvailable: false, out var telemetry);

        var first = await Event().ExecuteAsync(context);
        var second = await (Event() with { Id = Guid.NewGuid() }).ExecuteAsync(context);

        first.Should().BeTrue();
        second.Should().BeTrue();
        telemetry.GetSnapshot().LastOutcome.Should().Be(FuturesItiRuntimeOutcome.InputUnavailable);
        telemetry.GetSnapshot().EligibleEsTradeEvents.Should().Be(2);
        telemetry.GetSnapshot().CommandRequests.Should().Be(0);
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    static IFuturesItiSignalRealtimeContext Context(
        bool vxAvailable,
        out FuturesItiSignalRuntimeTelemetry telemetry)
    {
        var context = Substitute.For<IFuturesItiSignalRealtimeContext>();
        var market = Substitute.For<IMarketDataApi>();
        telemetry = new(TimeProvider.System);
        context.MarketDataApi.Returns(market);
        context.Telemetry.Returns(telemetry);
        context.GenerationGate.Returns(new FuturesItiSignalGenerationGate());
        context.HealthEvidence.Returns(new LivePipelineEvidence(TimeProvider.System));
        context.Logger.Returns(Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());
        market.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = Contract("ES", Es); return true; });
        market.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = Contract("VX", Vx); return true; });
        market.TryGetLastTickPrice(Vx, out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call =>
            {
                call[1] = vxAvailable ? Price(Vx, 22.75m) : default(FuturesMarketPriceSnapshot);
                return vxAvailable;
            });
        return context;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Event()
    {
        var entity = new TickDataEntityId(Es, Date, AssetTypeId.Futures);
        var timestamp = new DateTimeOffset(2026, 9, 14, 14, 30, 0, TimeSpan.Zero);
        return new()
        {
            Subject = new(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            Id = Guid.Parse("9a9115e5-b24d-424f-b431-95727c6aa1c7"),
            EntityId = entity,
            AggregateId = entity.Format(),
            EventSource = "bdd",
            ReceivedOn = timestamp.UtcDateTime,
            Price = Price(Es, 5450.25m),
            UpdateSource = FuturesMarketPriceUpdateSource.Trade
        };
    }

    static FuturesMarketPriceSnapshot Price(string contract, decimal value)
    {
        var timestamp = new DateTimeOffset(2026, 9, 14, 14, 30, 0, TimeSpan.Zero);
        return new(contract, 42, 7, AssetTypeId.Futures, Date, null,
            new(value, 5, 101, timestamp, timestamp, NormalizedTradeAction.New,
                NormalizedTradeSide.Buy, NormalizedTradeConditionFlags.None, Guid.Empty, 77));
    }

    static FuturesContractV3ReadModel Contract(string symbol, string contract) => new(
        contract, $"{symbol} future", symbol, symbol + "U6", "FUT", "USD",
        symbol == "VX" ? "CFE" : "CME", symbol == "VX" ? "1000" : "50",
        new DateOnly(2026, 9, 18), true);
}
