using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalRealtimeActorTests
{
    const string EsContractId = "ES20260918";
    const string VxContractId = "VX20260916";
    static readonly DateOnly ValueDate = new(2026, 8, 14);

    public sealed class TestableFuturesItiSignalRealtimeActor(
        IActorSupervisor supervisor,
        IActorMarketDataAnalyticsCommandApiFactory commandApiFactory,
        IMarketDataApi marketDataApi,
        IDbContextFactory dbFactory,
        ILogger<FuturesItiSignalRealtimeActor> logger)
        : FuturesItiSignalRealtimeActor(
            supervisor,
            commandApiFactory,
            marketDataApi,
            dbFactory,
            logger)
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
        var actor = CreateActor(out _, out _);
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
        var actor = CreateActor(out _, out _);

        var parsed = actor.Parse(Substitute.For<IEventActorContext>(), message);

        parsed.Should().BeSameAs(@event);
    }

    [Fact]
    public async Task Handler_CurrentEsAndActiveFreshVx_StartsAllThreeTimeFrames()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        ConfigureSuccessfulCommands(commandApi);
        var @event = CreateEvent();

        var handled = await @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        await commandApi.Received(1).GenerateFuturesItiSignalAsync(
            EsContractId,
            ValueDate,
            TimeFrameType.Daily,
            @event.Price.Trade!.Value.EventTimestamp.UtcDateTime,
            5450.25,
            22.75,
            null,
            ValueDate);
        await commandApi.Received(1).GenerateFuturesItiSignalAsync(
            EsContractId,
            ValueDate,
            TimeFrameType.Weekly,
            @event.Price.Trade!.Value.EventTimestamp.UtcDateTime,
            5450.25,
            22.75,
            null,
            ValueDate);
        await commandApi.Received(1).GenerateFuturesItiSignalAsync(
            EsContractId,
            ValueDate,
            TimeFrameType.Monthly,
            @event.Price.Trade!.Value.EventTimestamp.UtcDateTime,
            5450.25,
            22.75,
            null,
            ValueDate);
    }

    [Fact]
    public async Task Receive_CurrentEs_UsesActorBoundCommandApi()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var actor = CreateActor(out var factory, out var commandApi, marketDataApi);
        var context = Substitute.For<IEventActorContext>();

        await actor.Start(context);
        await actor.Receive(context, CreateEvent());
        await actor.Stop(context);

        factory.Received(1).Create(context);
        await commandApi.Received(1).GenerateFuturesItiSignalAsync(
            EsContractId,
            ValueDate,
            TimeFrameType.Daily,
            Arg.Any<DateTime>(),
            5450.25,
            22.75,
            null,
            ValueDate);
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
        var actor = CreateActor(out _, out _, marketDataApi);
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
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var @event = CreateEvent(contractId: VxContractId);

        var handled = await @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Handler_InactiveRequiredStream_DoesNotReadVxOrSendCommand(
        bool esActive,
        bool vxActive)
    {
        var marketDataApi = CreateReadyMarketDataApi(esActive, vxActive);
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();

        var handled = await CreateEvent().ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        _ = marketDataApi.DidNotReceiveWithAnyArgs().GetFuturesPriceAsync(default!);
        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Handler_MissingOrStaleVxPrice_DoesNotSendCommand()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        marketDataApi.GetFuturesPriceAsync(VxContractId)
            .Returns(Task.FromException<decimal>(
                new FuturesLastPriceUnavailableException(VxContractId)));
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();

        var handled = await CreateEvent().ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());

        handled.Should().BeTrue();
        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Handler_MissingCurrentEsContract_ThrowsConfigurationError()
    {
        var marketDataApi = Substitute.For<IMarketDataApi>();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();

        var action = () => CreateEvent().ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()).AsTask();

        await action.Should().ThrowAsync<FuturesContractRolloverConfigurationException>()
            .WithMessage("*current ES futures contract*");
        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Handler_MismatchedSnapshotIdentity_ThrowsMappingError()
    {
        var marketDataApi = CreateReadyMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var @event = CreateEvent() with
        {
            Price = CreateEvent().Price with { ContractId = "OTHER" }
        };

        var action = () => @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            marketDataApi,
            new FuturesItiSignalStreamOwnership(),
            CreateRealtimeState(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()).AsTask();

        await action.Should().ThrowAsync<MarketDataContractMappingException>();
        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default);
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
        out IActorMarketDataAnalyticsCommandApiFactory factory,
        out IActorMarketDataAnalyticsCommandApi commandApi,
        IMarketDataApi? marketDataApi = null)
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>())
            .Returns(Substitute.For<IActorMailbox>());
        commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        ConfigureSuccessfulCommands(commandApi);
        factory = Substitute.For<IActorMarketDataAnalyticsCommandApiFactory>();
        factory.Create(Arg.Any<IEventActorContext>()).Returns(commandApi);
        return new TestableFuturesItiSignalRealtimeActor(
            supervisor,
            factory,
            marketDataApi ?? Substitute.For<IMarketDataApi>(),
            CreateDbFactory(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());
    }

    static void ConfigureSuccessfulCommands(IActorMarketDataAnalyticsCommandApi commandApi)
        => commandApi.GenerateFuturesItiSignalAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<DateTime>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateOnly?>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()))));

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
        api.GetFuturesPriceAsync(VxContractId).Returns(Task.FromResult(22.75m));
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
