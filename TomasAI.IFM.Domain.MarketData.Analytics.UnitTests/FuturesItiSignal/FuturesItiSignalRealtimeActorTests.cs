using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalRealtimeActorTests
{
    const string EsContractId = "ES20260918";
    const string VxContractId = "VX20260916";
    static readonly DateOnly ValueDate = new(2026, 8, 14);

    public sealed class TestableFuturesItiSignalRealtimeActor(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesItiSignalRealtimeActor> projector,
        IMarketDataApi marketDataApi,
        IDbContextFactory dbFactory,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesItiSignalRealtimeActor> logger)
        : FuturesItiSignalRealtimeActor(new FuturesItiSignalRealtimeContext(
            supervisor,
            projector,
            marketDataApi,
            dbFactory,
            statusConsoleWriter,
            logger))
    {
        public IEvent Parse(IEventActorContext context, IActorMessage message) =>
            ParseMessage(context, message);

        public ValueTask Receive(IEventActorContext context, IEvent @event) =>
            ReceiveAsync(context, @event);

        public ValueTask Start(IEventActorContext context) => OnStartup(context);
        public ValueTask Stop(IEventActorContext context) => OnShutdown(context);
    }

    [Fact]
    public async Task Lifecycle_RegistersAndRemovesMarketPriceRealtimeRoute()
    {
        var context = Substitute.For<IEventActorContext>();
        var actor = CreateActor(out _);
        var route = new ActorTypeId(
            ActorType.Realtime,
            FuturesMarketPriceUpdatedRealtimeEvent.Actor,
            FuturesMarketPriceUpdatedRealtimeEvent.Verb);

        await actor.Start(context);
        await actor.Stop(context);

        context.Received(1).AddRealtimeRouter(route, actor.Id);
        context.Received(1).RemoveRealtimeRouter(route, actor.Id);
        actor.Id.Should().Be(new ActorMailboxId(
            ActorType.Realtime,
            FuturesItiSignalRealtimeActor.ActorName));
    }

    [Fact]
    public void ParseMessage_RoutedMarketPriceEvent_ReturnsConcreteEvent()
    {
        var @event = CreateEvent();
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(new ActorSubject(
            ActorType.Realtime,
            FuturesItiSignalRealtimeActor.ActorName,
            FuturesMarketPriceUpdatedRealtimeEvent.Verb,
            @event.EntityId.Format()));
        message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>().Returns(@event);
        var actor = CreateActor(out _);

        var parsed = actor.Parse(Substitute.For<IEventActorContext>(), message);

        parsed.Should().BeSameAs(@event);
    }

    [Fact]
    public async Task Handler_CurrentEsAndActiveFreshVx_StartsAllThreeTimeFrames()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var projector = CreateProjector();
        var @event = CreateEvent();

        var handled = await @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            projector,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        var generated = projector.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IRealtimeProjector<FuturesItiSignalRealtimeActor>.ProcessRealtimeEventAsync))
            .Select(call => call.GetArguments()[0])
            .Cast<FuturesItiSignalGeneratedEvent>()
            .ToArray();
        generated.Should().HaveCount(3);
        generated.Select(item => item.FuturesItiSignal!.TimePeriod).Should().BeEquivalentTo([
            TimeFrameType.Daily,
            TimeFrameType.Weekly,
            TimeFrameType.Monthly]);
        generated.Should().OnlyContain(item =>
            item.Subject.ActorType == ActorType.Realtime
            && item.Subject.Name == FuturesItiSignalRealtimeActor.ActorName
            && item.FuturesItiSignal!.IntrinsicTime == @event.Price.Trade!.Value.EventTimestamp.UtcDateTime
            && item.FuturesItiSignal.IntrinsicPrice == 5450.25
            && item.VixFuturesPrice == 22.75);
    }

    [Fact]
    public async Task Receive_CurrentEs_UsesActorBoundRealtimeProjector()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var actor = CreateActor(out var projector, marketDataApi);
        var context = Substitute.For<IEventActorContext>();

        await actor.Start(context);
        await actor.Receive(context, CreateEvent());
        await actor.Stop(context);

        await projector.Received(1).StartAsync(context, Arg.Any<CancellationToken>());
        await projector.Received(3).ProcessRealtimeEventAsync(
            Arg.Any<FuturesItiSignalGeneratedEvent>(),
            Arg.Any<CancellationToken>());
        await projector.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await marketDataApi.Received(1).StartStreamingFuturesTickDataAsync(
            EsContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "ES"));
        await marketDataApi.Received(1).StartStreamingFuturesTickDataAsync(
            VxContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "VX"));
        await marketDataApi.Received(1).StopStreamingFuturesTickDataAsync(
            VxContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "VX"));
        await marketDataApi.Received(1).StopStreamingFuturesTickDataAsync(
            EsContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "ES"));
    }

    [Fact]
    public async Task Receive_RepeatedEsUpdates_AcquiresEachStableStreamOwnerOnce()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var actor = CreateActor(out _, marketDataApi);
        var context = Substitute.For<IEventActorContext>();

        await actor.Start(context);
        await actor.Receive(context, CreateEvent());
        await actor.Receive(context, CreateEvent());

        await marketDataApi.Received(1).StartStreamingFuturesTickDataAsync(
            EsContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "ES"));
        await marketDataApi.Received(1).StartStreamingFuturesTickDataAsync(
            VxContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "VX"));
    }

    [Fact]
    public async Task StreamOwnership_VxAcquisitionFailure_RollsBackEsRegistration()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var esOwner = new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "ES");
        var vxOwner = new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "VX");
        marketDataApi.StartStreamingFuturesTickDataAsync(EsContractId, esOwner)
            .Returns(Task.FromResult(true));
        marketDataApi.StartStreamingFuturesTickDataAsync(VxContractId, vxOwner)
            .Returns(Task.FromException<bool>(new InvalidOperationException("VX route failed")));
        var ownership = new FuturesItiSignalStreamOwnership();

        var action = () => ownership.EnsureAsync(marketDataApi).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("VX route failed");
        await marketDataApi.Received(1).StopStreamingFuturesTickDataAsync(
            EsContractId,
            esOwner);
    }

    [Fact]
    public async Task StreamOwnership_Rollover_AcquiresNewContractsAndReleasesOldContracts()
    {
        const string nextEsContractId = "ES20261218";
        const string nextVxContractId = "VX20261021";
        var rolled = false;
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.TryGetCurrentlyTradedFuturesContract(
                "ES",
                out Arg.Any<FuturesContractV2ReadModel>()!)
            .Returns(call =>
            {
                call[1] = rolled
                    ? Contract("ES", nextEsContractId, "ESZ6", new DateOnly(2026, 12, 18))
                    : Contract("ES", EsContractId, "ESU6", new DateOnly(2026, 9, 18));
                return true;
            });
        marketDataApi.TryGetCurrentlyTradedFuturesContract(
                "VX",
                out Arg.Any<FuturesContractV2ReadModel>()!)
            .Returns(call =>
            {
                call[1] = rolled
                    ? Contract("VX", nextVxContractId, "VXV6", new DateOnly(2026, 10, 21))
                    : Contract("VX", VxContractId, "VXU6", new DateOnly(2026, 9, 16));
                return true;
            });
        marketDataApi.IsTickDataStreamActive(Arg.Any<string>()).Returns(true);
        var ownership = new FuturesItiSignalStreamOwnership();

        _ = await ownership.EnsureAsync(marketDataApi);
        rolled = true;
        _ = await ownership.EnsureAsync(marketDataApi);

        await marketDataApi.Received(1).StartStreamingFuturesTickDataAsync(
            nextEsContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "ES"));
        await marketDataApi.Received(1).StartStreamingFuturesTickDataAsync(
            nextVxContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "VX"));
        await marketDataApi.Received(1).StopStreamingFuturesTickDataAsync(
            EsContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "ES"));
        await marketDataApi.Received(1).StopStreamingFuturesTickDataAsync(
            VxContractId,
            new TickerStreamOwner("FuturesItiSignal", "CurrentContracts", "VX"));
    }

    [Fact]
    public async Task Handler_NonCurrentEsOrVxUpdate_DoesNotSendCommand()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var projector = CreateProjector();
        var @event = CreateEvent(contractId: VxContractId);

        var handled = await @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            projector,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        await projector.DidNotReceiveWithAnyArgs().ProcessRealtimeEventAsync(default!);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Handler_InactiveRequiredStream_DoesNotReadVxOrSendCommand(
        bool esActive,
        bool vxActive)
    {
        var marketDataApi = CreateReadyMarketDataApi(esActive, vxActive);
        var projector = CreateProjector();

        var handled = await CreateEvent().ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            projector,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        _ = marketDataApi.DidNotReceiveWithAnyArgs().GetFuturesPriceAsync(default!);
        await projector.DidNotReceiveWithAnyArgs().ProcessRealtimeEventAsync(default!);
    }

    [Fact]
    public async Task Handler_MissingOrStaleVxPrice_DoesNotSendCommand()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        marketDataApi.GetFuturesPriceAsync(VxContractId)
            .Returns(Task.FromResult<decimal?>(null));
        var projector = CreateProjector();

        var handled = await CreateEvent().ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            projector,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        await projector.DidNotReceiveWithAnyArgs().ProcessRealtimeEventAsync(default!);
    }

    [Fact]
    public async Task Handler_MissingCurrentEsContract_ThrowsConfigurationError()
    {
        var marketDataApi = Substitute.For<IMarketDataApi>();
        var projector = CreateProjector();

        var action = () => CreateEvent().ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            projector,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()).AsTask();

        await action.Should().ThrowAsync<FuturesContractRolloverConfigurationException>()
            .WithMessage("*current ES futures contract*");
        await projector.DidNotReceiveWithAnyArgs().ProcessRealtimeEventAsync(default!);
    }

    [Fact]
    public async Task Handler_MismatchedSnapshotIdentity_ThrowsMappingError()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var projector = CreateProjector();
        var @event = CreateEvent() with
        {
            Price = CreateEvent().Price with { ContractId = "OTHER" }
        };

        var action = () => @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            projector,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()).AsTask();

        await action.Should().ThrowAsync<MarketDataContractMappingException>();
        await projector.DidNotReceiveWithAnyArgs().ProcessRealtimeEventAsync(default!);
    }

    [Fact]
    public void ActorAssembly_ExposesRealtimeActorForReflectionRegistration()
    {
        var actorType = typeof(FuturesItiSignalRealtimeActor);

        MarketDataAnalyticsActorAssembly.Current.GetTypes().Should().Contain(actorType);
        actorType.GetInterfaces().Should().Contain(contract =>
            contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IActor<>));
    }

    static TestableFuturesItiSignalRealtimeActor CreateActor(
        out IRealtimeProjector<FuturesItiSignalRealtimeActor> projector,
        IMarketDataApi? marketDataApi = null)
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>())
            .Returns(Substitute.For<IActorMailbox>());
        projector = CreateProjector();
        return new TestableFuturesItiSignalRealtimeActor(
            supervisor,
            projector,
            marketDataApi ?? Substitute.For<IMarketDataApi>(),
            CreateDbFactory(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());
    }

    static IRealtimeProjector<FuturesItiSignalRealtimeActor> CreateProjector()
    {
        var projector = Substitute.For<IRealtimeProjector<FuturesItiSignalRealtimeActor>>();
        projector.ProcessRealtimeEventAsync(
                Arg.Any<IEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        return projector;
    }

    static FuturesItiSignalRealtimeState CreateRealtimeState()
        => new(CreateDbFactory());

    static IDbContextFactory CreateDbFactory()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        marketDataDb.GetFuturesItiTimeFrameStateAsync(
                Arg.Any<string>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(null));
        marketDataDb.GetFuturesItiSignalsForContractAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FuturesItiSignalV2ReadModel>>([]));
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(marketDataDb);
        return factory;
    }

    static IMarketDataApi CreateReadyMarketDataApi(
        bool esActive = true,
        bool vxActive = true)
    {
        var api = Substitute.For<IMarketDataApi>();
        var es = Contract("ES", EsContractId, "ESU6", new DateOnly(2026, 9, 18));
        var vx = Contract("VX", VxContractId, "VXU6", new DateOnly(2026, 9, 16));
        api.TryGetCurrentlyTradedFuturesContract("ES", out Arg.Any<FuturesContractV2ReadModel>()!)
            .Returns(call =>
            {
                call[1] = es;
                return true;
            });
        api.TryGetCurrentlyTradedFuturesContract("VX", out Arg.Any<FuturesContractV2ReadModel>()!)
            .Returns(call =>
            {
                call[1] = vx;
                return true;
            });
        api.IsTickDataStreamActive(EsContractId).Returns(esActive);
        api.IsTickDataStreamActive(VxContractId).Returns(vxActive);
        api.GetFuturesPriceAsync(VxContractId).Returns(Task.FromResult<decimal?>(22.75m));
        return api;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent(
        string contractId = EsContractId)
    {
        var entityId = new TickDataEntityId(contractId, ValueDate, AssetTypeId.Futures);
        var timestamp = new DateTimeOffset(2026, 8, 14, 14, 30, 0, TimeSpan.Zero);
        return new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = timestamp.UtcDateTime,
            Price = new FuturesMarketPriceSnapshot(
                contractId,
                42,
                7,
                AssetTypeId.Futures,
                ValueDate,
                null,
                new FuturesMarketTradeSnapshot(
                    5450.25m,
                    5,
                    101,
                    timestamp,
                    timestamp.AddMilliseconds(2)))
        };
    }

    static FuturesContractV2ReadModel Contract(
        string symbol,
        string contractId,
        string localSymbol,
        DateOnly maturity) => new(
            contractId,
            $"{symbol} future",
            symbol,
            localSymbol,
            "FUT",
            "USD",
            symbol == "VX" ? "CFE" : "CME",
            symbol == "VX" ? "1000" : "50",
            maturity,
            true);
}
