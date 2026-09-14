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
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalRealtimeActorTests
{
    const string EsContractId = "ES20260918";
    const string VxContractId = "VX20260916";
    static readonly DateOnly ValueDate = new(2026, 9, 14);

    sealed class TestActor(IRealtimeActorContext<FuturesItiSignalRealtimeActor> context)
        : FuturesItiSignalRealtimeActor(context)
    {
        public ValueTask Start(IEventActorContext<FuturesItiSignalRealtimeActor> context) => OnStartup(context);
        public ValueTask Stop(IEventActorContext<FuturesItiSignalRealtimeActor> context) => OnShutdown(context);
        public IEvent Parse(IEventActorContext<FuturesItiSignalRealtimeActor> context, IActorMessage message) =>
            ParseMessage(context, message);
    }

    [Fact]
    public async Task LifecycleRegistersOnlyMarketPriceRoute()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorMailbox>());
        var context = new FuturesItiSignalRealtimeContext(
            supervisor, Substitute.For<IMarketDataApi>(), new LivePipelineEvidence(TimeProvider.System),
            new FuturesItiSignalRuntimeTelemetry(TimeProvider.System),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());
        var actor = new TestActor(context);
        var eventContext = Substitute.For<IEventActorContext<FuturesItiSignalRealtimeActor>>();
        var route = new ActorTypeId(ActorType.Realtime,
            FuturesMarketPriceUpdatedRealtimeEvent.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb);

        await actor.Start(eventContext);
        await actor.Stop(eventContext);

        eventContext.Received(1).AddRealtimeRouter(route, actor.Id);
        eventContext.Received(1).RemoveRealtimeRouter(route, actor.Id);
    }

    [Fact]
    public void ActorParsesMarketPriceEventThroughItsSingleMapping()
    {
        var @event = Event();
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(new ActorSubject(
            ActorType.Realtime, FuturesItiSignalRealtimeActor.ActorName,
            FuturesMarketPriceUpdatedRealtimeEvent.Verb, @event.EntityId.Format()));
        message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>().Returns(@event);
        var actor = Actor();

        actor.Parse(Substitute.For<IEventActorContext<FuturesItiSignalRealtimeActor>>(), message)
            .Should().BeSameAs(@event);
    }

    [Fact]
    public async Task EligibleCurrentEsTradeRequestsExactlyOneDailyCommand()
    {
        var context = Context(out _, out _);
        GenerateFuturesItiSignalCommand? sent = null;
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Do<GenerateFuturesItiSignalCommand>(command => sent = command))
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        var @event = Event();

        var handled = await @event.ExecuteAsync(context);
        await context.GenerationGate.WaitForIdleAsync();

        handled.Should().BeTrue();
        sent.Should().NotBeNull();
        sent!.TimePeriod.Should().Be(TimeFrameType.Daily);
        sent.TimeFrameStartValueDate.Should().Be(ValueDate);
        sent.EntityId.Should().Be(new FuturesItiSignalEntityId(EsContractId, ValueDate, TimeFrameType.Daily));
        sent.FuturesPrice.Should().Be(5450.25);
        sent.VixFuturesPrice.Should().Be(22.75);
        sent.CommandId.Should().Be(@event.Id);
        await context.Received(1).RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
            Arg.Any<GenerateFuturesItiSignalCommand>());
    }

    [Fact]
    public async Task MissingVxPriceIsDegradedAndDoesNotRequestACommand()
    {
        var context = Context(out var marketData, out var telemetry, includeVxPrice: false);

        var handled = await Event().ExecuteAsync(context);

        handled.Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
        telemetry.GetSnapshot().LastOutcome.Should().Be(FuturesItiRuntimeOutcome.InputUnavailable);
        _ = marketData.Received(1).TryGetLastTickPrice(VxContractId, out Arg.Any<FuturesMarketPriceSnapshot>());
    }

    [Fact]
    public async Task LaterValidVxPriceClearsInputDegradationOnNextEsTrade()
    {
        var context = Context(out var marketData, out var telemetry, includeVxPrice: false);
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        await Event().ExecuteAsync(context);
        marketData.TryGetLastTickPrice(VxContractId, out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call => { call[1] = Price(VxContractId, 22.75m); return true; });

        var handled = await Event().ExecuteAsync(context);
        await context.GenerationGate.WaitForIdleAsync();

        handled.Should().BeTrue();
        telemetry.GetSnapshot().LastOutcome.Should().Be(FuturesItiRuntimeOutcome.CommandAccepted);
        telemetry.GetSnapshot().CommandRequests.Should().Be(1);
    }

    [Theory]
    [InlineData(FuturesMarketPriceUpdateSource.Quote)]
    [InlineData(FuturesMarketPriceUpdateSource.Unknown)]
    public async Task NonTradeUpdateIsIgnoredWithoutCommand(FuturesMarketPriceUpdateSource source)
    {
        var context = Context(out _, out var telemetry);

        var handled = await (Event() with { UpdateSource = source }).ExecuteAsync(context);

        handled.Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
        telemetry.GetSnapshot().FilteredEvents.Should().Be(1);
    }

    [Fact]
    public async Task NonCurrentEsTradeIsIgnoredWithoutCommand()
    {
        var context = Context(out _, out var telemetry);
        var other = Event();
        var otherEntity = new TickDataEntityId("NQ20260918", ValueDate, AssetTypeId.Futures);
        other = other with
        {
            EntityId = otherEntity,
            Price = Price(otherEntity.ContractId, 20_000m),
            Subject = new ActorSubject(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, otherEntity.Format())
        };

        (await other.ExecuteAsync(context)).Should().BeTrue();

        telemetry.GetSnapshot().FilteredEvents.Should().Be(1);
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Fact]
    public async Task MissingTradeIsIgnoredWithoutCommand()
    {
        var context = Context(out _, out var telemetry);
        var @event = Event();
        @event = @event with { Price = @event.Price with { Trade = null } };

        (await @event.ExecuteAsync(context)).Should().BeTrue();

        telemetry.GetSnapshot().FilteredEvents.Should().Be(1);
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Fact]
    public async Task ConflictingEntityAndSnapshotAreRejectedWithoutThrowing()
    {
        var context = Context(out _, out var telemetry);
        var @event = Event();
        @event = @event with { Price = @event.Price with { ContractId = "NQ20260918" } };

        var handled = await @event.ExecuteAsync(context);

        handled.Should().BeFalse();
        telemetry.GetSnapshot().LastOutcome.Should().Be(FuturesItiRuntimeOutcome.Failed);
        context.Logger.ReceivedCalls()
            .Any(call => call.GetMethodInfo().Name == nameof(ILogger.Log)
                && call.GetArguments()[1] is EventId { Id: 23809 })
            .Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Fact]
    public async Task RejectedDailyCommandRecordsFailureAfterIngressReturns()
    {
        var context = Context(out _, out var telemetry);
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(new ServiceFailed<GuidResult>(GenerateFuturesItiSignalCommand.ErrorId, "rejected"));

        var handled = await Event().ExecuteAsync(context);
        await context.GenerationGate.WaitForIdleAsync();

        handled.Should().BeTrue();
        telemetry.GetSnapshot().Failures.Should().Be(1);
        telemetry.GetSnapshot().LastReason.Should().Be("rejected");
    }

    [Fact]
    public async Task TicksReceivedWhileGenerationIsBusyAreIgnoredWithoutQueueingAnotherCommand()
    {
        var context = Context(out _, out var telemetry);
        var completion = new TaskCompletionSource<ServiceResult<GuidResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(_ => new ValueTask<ServiceResult<GuidResult>>(completion.Task));

        (await Event().ExecuteAsync(context)).Should().BeTrue();
        for (var index = 0; index < 1_000; index++)
            (await (Event() with { Id = Guid.NewGuid() }).ExecuteAsync(context)).Should().BeTrue();

        await context.Received(1).RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
            Arg.Any<GenerateFuturesItiSignalCommand>());
        telemetry.GetSnapshot().BusySkippedEvents.Should().Be(1_000);

        completion.SetResult(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        await context.GenerationGate.WaitForIdleAsync();
        context.GenerationGate.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task GenerationFailureReleasesGateForTheNextTick()
    {
        var context = Context(out _, out var telemetry);
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(_ => ValueTask.FromException<ServiceResult<GuidResult>>(
                new InvalidOperationException("request failed")));

        (await Event().ExecuteAsync(context)).Should().BeTrue();
        await context.GenerationGate.WaitForIdleAsync();

        context.GenerationGate.IsBusy.Should().BeFalse();
        telemetry.GetSnapshot().Failures.Should().Be(1);
        telemetry.GetSnapshot().LastReason.Should().Be("request failed");
    }

    [Theory]
    [InlineData(NormalizedTradeAction.Cancel)]
    [InlineData(NormalizedTradeAction.Clear)]
    public async Task NonPriceTradeActionIsIgnoredWithoutCommand(NormalizedTradeAction action)
    {
        var context = Context(out _, out _);
        var @event = Event();
        @event = @event with
        {
            Price = @event.Price with { Trade = @event.Price.Trade!.Value with { NormalizedTradeAction = action } }
        };

        (await @event.ExecuteAsync(context)).Should().BeTrue();

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Fact]
    public void EmptySourceIdUsesStableCommandIdentity()
    {
        var @event = Event() with { Id = Guid.Empty };
        var trade = @event.Price.Trade!.Value;

        var first = FuturesMarketPriceUpdated.CreateCommandId(@event, trade);
        var second = FuturesMarketPriceUpdated.CreateCommandId(@event, trade);

        first.Should().NotBe(Guid.Empty);
        second.Should().Be(first);
    }

    [Fact]
    public void RealtimeContextExposesNoPersistenceOrProjectionDependency()
    {
        var properties = typeof(IFuturesItiSignalRealtimeContext).GetProperties()
            .Select(property => property.PropertyType.Name)
            .ToArray();

        properties.Should().NotContain(name => name.Contains("DbContext", StringComparison.Ordinal)
            || name.Contains("Projector", StringComparison.Ordinal)
            || name.Contains("Repository", StringComparison.Ordinal));
    }

    static TestActor Actor()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorMailbox>());
        return new(new FuturesItiSignalRealtimeContext(
            supervisor, Substitute.For<IMarketDataApi>(), new LivePipelineEvidence(TimeProvider.System),
            new FuturesItiSignalRuntimeTelemetry(TimeProvider.System),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()));
    }

    static IFuturesItiSignalRealtimeContext Context(
        out IMarketDataApi marketData,
        out FuturesItiSignalRuntimeTelemetry telemetry,
        bool includeVxPrice = true)
    {
        var context = Substitute.For<IFuturesItiSignalRealtimeContext>();
        marketData = Substitute.For<IMarketDataApi>();
        telemetry = new FuturesItiSignalRuntimeTelemetry(TimeProvider.System);
        context.MarketDataApi.Returns(marketData);
        context.Telemetry.Returns(telemetry);
        context.GenerationGate.Returns(new FuturesItiSignalGenerationGate());
        context.HealthEvidence.Returns(new LivePipelineEvidence(TimeProvider.System));
        var logger = Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        context.Logger.Returns(logger);
        var es = Contract("ES", EsContractId, "ESU6", new DateOnly(2026, 9, 18));
        var vx = Contract("VX", VxContractId, "VXU6", new DateOnly(2026, 9, 16));
        marketData.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = es; return true; });
        marketData.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = vx; return true; });
        marketData.TryGetLastTickPrice(VxContractId, out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call =>
            {
                call[1] = includeVxPrice ? Price(VxContractId, 22.75m) : default(FuturesMarketPriceSnapshot);
                return includeVxPrice;
            });
        return context;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Event()
    {
        var entity = new TickDataEntityId(EsContractId, ValueDate, AssetTypeId.Futures);
        var timestamp = new DateTimeOffset(2026, 9, 14, 14, 30, 0, TimeSpan.Zero);
        return new()
        {
            Subject = new ActorSubject(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            Id = Guid.Parse("b3510c41-a94a-41a5-87fe-6d67f93245f9"),
            EntityId = entity,
            CommandId = Guid.Parse("8f7db79f-7fa3-4a96-8375-245acb8392e3"),
            AggregateId = entity.Format(),
            EventSource = "unit-test",
            ReceivedOn = timestamp.UtcDateTime,
            Price = Price(EsContractId, 5450.25m),
            UpdateSource = FuturesMarketPriceUpdateSource.Trade
        };
    }

    static FuturesMarketPriceSnapshot Price(string contractId, decimal value)
    {
        var timestamp = new DateTimeOffset(2026, 9, 14, 14, 30, 0, TimeSpan.Zero);
        return new(contractId, 42, 7, AssetTypeId.Futures, ValueDate, null,
            new FuturesMarketTradeSnapshot(value, 5, 101, timestamp, timestamp.AddMilliseconds(2),
                NormalizedTradeAction.New, NormalizedTradeSide.Buy,
                NormalizedTradeConditionFlags.None,
                Guid.Parse("bb23477b-f9e1-44c3-89d1-18e8e2adf830"), 77));
    }

    static FuturesContractV3ReadModel Contract(
        string symbol, string contractId, string localSymbol, DateOnly maturity) => new(
            contractId, $"{symbol} future", symbol, localSymbol, "FUT", "USD",
            symbol == "VX" ? "CFE" : "CME", symbol == "VX" ? "1000" : "50", maturity, true);
}
