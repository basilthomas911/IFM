using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

/// <summary>
/// Exercises the complete ITI boundary from routed realtime actors through the
/// no-replay realtime projector and Scylla storage. The adjacent realtime-actor
/// integration test owns the Core NATS routing boundary.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ItiPipelineIntegrationCollection.Name)]
public sealed class FuturesItiSignalRealtimePipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>,
      IClassFixture<MarketDataAnalyticsFixture>
{
    const string EsContractId = "ES-ITI-REALTIME-PIPELINE";
    const string VxContractId = "VX-ITI-REALTIME-PIPELINE";
    const double EsPrice = 5450.25;
    const double VxPrice = 22.75;
    static readonly DateOnly ValueDate = new(2026, 8, 14);
    static readonly DateTime FirstTimestamp = new(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);
    static readonly TimeFrameType[] ExpectedPeriods =
    [
        TimeFrameType.Daily,
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    ];

    readonly IActorProducer _durableProducer =
        factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task RoutedTrades_ProjectDailyWeeklyAndMonthlySignalsWithoutReplay()
    {
        await ResetAsync();
        var marketDataApi = CreateReadyMarketDataApi();
        await using var probe = await ItiEventProbe.StartAsync(EsContractId, ValueDate);
        await using var realtime = await RealtimeHarness.StartAsync(
            _durableProducer,
            marketDataApi,
            dbFixture.DbFactory);

        try
        {
            await realtime.PublishAsync(CreateEvent(101, FirstTimestamp));
            realtime.RealtimeProjections.Should().Be(3);
            await probe.WaitForCompletionCountAsync(3);

            await AssertStoredSignalsAsync(expectedCount: 1, FirstTimestamp);
            AssertGeneratedEvents(probe, expectedCount: 1);

            // An unchanged trade updates hot observation state but remains inside
            // every active publication band.
            var secondTimestamp = FirstTimestamp.AddSeconds(1);
            await realtime.PublishAsync(CreateEvent(102, secondTimestamp));
            await Task.Delay(500);

            await AssertStoredSignalsAsync(expectedCount: 1, FirstTimestamp);
            AssertGeneratedEvents(probe, expectedCount: 1);
            realtime.RealtimeProjections.Should().Be(3);

            var bandTimestamp = FirstTimestamp.AddSeconds(2);
            const double bandCrossingPrice = EsPrice + 10;
            await realtime.PublishAsync(CreateEvent(
                103,
                bandTimestamp,
                price: bandCrossingPrice));
            realtime.RealtimeProjections.Should().Be(6);
            await probe.WaitForCompletionCountAsync(6);

            foreach (var period in ExpectedPeriods)
            {
                var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(
                    new FuturesItiSignalEntityId(EsContractId, ValueDate, period));
                signals.Should().HaveCount(2);
                signals.Should().Contain(signal =>
                    signal.IntrinsicPrice == bandCrossingPrice
                    && signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
                    && signal.IntrinsicTimeGroupId == 0);
                var current = await dbFixture.MarketDataDb.GetFuturesItiTimeFrameStateAsync(
                    EsContractId,
                    period,
                    FuturesItiCalendarBucketStart(ValueDate, period));
                current.Should().NotBeNull();
                current!.IntrinsicPrice.Should().Be(bandCrossingPrice);
                current.TimeFrameStartValueDate.Should().Be(ValueDate);
                current.BandAnchorPrice.Should().Be(bandCrossingPrice);
                current.BandPercentage.Should().Be(0.10);
                current.BandSize.Should().BeGreaterThan(0);
            }
            realtime.RealtimeProjections.Should().Be(6);
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Theory]
    [InlineData("non-current-es")]
    [InlineData("inactive-es")]
    [InlineData("inactive-vx")]
    [InlineData("missing-vx-price")]
    [InlineData("stopping-api")]
    public async Task IneligibleCoreNatsTrade_DoesNotCreateRealtimeItiSignals(
        string condition)
    {
        await ResetAsync();
        var marketDataApi = CreateReadyMarketDataApi();
        var eventContractId = EsContractId;
        switch (condition)
        {
            case "non-current-es":
                eventContractId = "ES-ITI-NON-CURRENT";
                break;
            case "inactive-es":
                marketDataApi.IsTickDataStreamActive(EsContractId).Returns(false);
                break;
            case "inactive-vx":
                marketDataApi.IsTickDataStreamActive(VxContractId).Returns(false);
                break;
            case "missing-vx-price":
                marketDataApi.GetFuturesPriceAsync(VxContractId)
                    .Returns(Task.FromResult<decimal?>(null));
                break;
            case "stopping-api":
                marketDataApi.IsTickDataStreamActive(EsContractId).Returns(false);
                marketDataApi.StartStreamingFuturesTickDataAsync(
                        EsContractId,
                        Arg.Any<TickerStreamOwner?>())
                    .Returns(Task.FromException<bool>(new MarketDataApiNotRunningException()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition));
        }

        await using var realtime = await RealtimeHarness.StartAsync(
            _durableProducer,
            marketDataApi,
            dbFixture.DbFactory);
        try
        {
            await realtime.PublishAsync(CreateEvent(201, FirstTimestamp, eventContractId));
            await Task.Delay(TimeSpan.FromSeconds(1));

            realtime.RealtimeProjections.Should().Be(0);
            foreach (var period in ExpectedPeriods)
            {
                var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(
                    new FuturesItiSignalEntityId(EsContractId, ValueDate, period));
                signals.Should().BeEmpty();
            }
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Fact]
    public async Task IndependentPeriodCommandIds_AreDurablyIdempotent()
    {
        await ResetAsync();
        await using var probe = await ItiEventProbe.StartAsync(
            EsContractId,
            ValueDate,
            ActorType.Event,
            FuturesItiSignalGeneratedEvent.Actor);
        var commandIds = new Dictionary<TimeFrameType, Guid>
        {
            [TimeFrameType.Weekly] = Guid.NewGuid(),
            [TimeFrameType.Monthly] = Guid.NewGuid()
        };

        try
        {
            foreach (var period in new[] { TimeFrameType.Weekly, TimeFrameType.Monthly })
            {
                var result = await SendDurableItiCommandAsync(
                    period,
                    commandIds[period]);
                result.Success.Should().BeTrue();
            }
            await probe.WaitForLongerPeriodCompletionsAsync();

            // A redelivered command with the same ID remains idempotent.
            foreach (var period in new[] { TimeFrameType.Weekly, TimeFrameType.Monthly })
            {
                var result = await SendDurableItiCommandAsync(
                    period,
                    commandIds[period]);
                result.Success.Should().BeTrue(
                    "an idempotent duplicate must be acknowledged without being processed again");
                result.Value!.Guid.Should().Be(commandIds[period]);
            }
            await Task.Delay(TimeSpan.FromSeconds(1));

            foreach (var period in new[] { TimeFrameType.Weekly, TimeFrameType.Monthly })
            {
                probe.GeneratedFor(period).Should().ContainSingle();
                var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(
                    new FuturesItiSignalEntityId(EsContractId, ValueDate, period));
                signals.Should().ContainSingle();
                signals.Single().TradingDays.Should().Be(TradingDays(period));
            }

            probe.GeneratedFor(TimeFrameType.Daily).Should().BeEmpty();
            var daily = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(
                new FuturesItiSignalEntityId(EsContractId, ValueDate, TimeFrameType.Daily));
            daily.Should().BeEmpty();
        }
        finally
        {
            await ResetAsync();
        }
    }

    ValueTask<ServiceResult<GuidResult>> SendDurableItiCommandAsync(
        TimeFrameType period,
        Guid commandId)
    {
        var entityId = new FuturesItiSignalEntityId(EsContractId, ValueDate, period);
        var command = new GenerateFuturesItiSignalCommand(
            EsContractId,
            ValueDate,
            period,
            FirstTimestamp,
            EsPrice,
            VxPrice)
        {
            CommandId = commandId,
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesItiSignalCommand.Actor,
                GenerateFuturesItiSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesItiSignalCommand.ErrorId
        };
        return _durableProducer.RequestAsync<
            GenerateFuturesItiSignalCommand,
            FuturesItiSignalEntityId,
            GuidResult>(command.Subject, command, entityId);
    }

    async Task AssertStoredSignalsAsync(int expectedCount, params DateTime[] timestamps)
    {
        foreach (var period in ExpectedPeriods)
        {
            var entityId = new FuturesItiSignalEntityId(EsContractId, ValueDate, period);
            var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(entityId);
            signals.Should().HaveCount(expectedCount);
            signals.Should().OnlyContain(signal => signal.TradingDays == TradingDays(period));
            signals.Should().OnlyContain(signal => signal.IntrinsicPrice == EsPrice);
            signals.Select(signal => signal.IntrinsicTime)
                .Should().BeEquivalentTo(timestamps);
        }
    }

    static void AssertGeneratedEvents(ItiEventProbe probe, int expectedCount)
    {
        foreach (var period in ExpectedPeriods)
        {
            var events = probe.GeneratedFor(period);
            events.Should().HaveCount(expectedCount);
            events.Should().OnlyContain(@event => @event.VixFuturesPrice == VxPrice);
            events.Should().OnlyContain(@event =>
                @event.FuturesItiSignal != null
                && @event.FuturesItiSignal.TradingDays == TradingDays(period)
                && @event.FuturesItiSignal.IntrinsicPrice == EsPrice);
        }
    }

    async Task ResetAsync()
    {
        foreach (var period in ExpectedPeriods)
        {
            var entityId = new FuturesItiSignalEntityId(EsContractId, ValueDate, period);
            var subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesItiSignalCommand.Actor,
                GenerateFuturesItiSignalCommand.Verb,
                entityId.Format());
            var streamId = await dbFixture.ActorEventSourceDb
                .GetEventStreamIdAsync($"{subject.ThreadId}");
            if (streamId > 0)
                await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(streamId);
            await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(
                EsContractId,
                ValueDate,
                period);
        }
    }

    static int TradingDays(TimeFrameType period) => period switch
    {
        TimeFrameType.Daily => 1,
        TimeFrameType.Weekly => 5,
        TimeFrameType.Monthly => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    static DateOnly FuturesItiCalendarBucketStart(DateOnly valueDate, TimeFrameType period)
        => period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };

    static IMarketDataApi CreateReadyMarketDataApi()
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
        api.IsTickDataStreamActive(EsContractId).Returns(true);
        api.IsTickDataStreamActive(VxContractId).Returns(true);
        api.GetFuturesPriceAsync(VxContractId).Returns(Task.FromResult<decimal?>((decimal)VxPrice));
        return api;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent(
        long sourceSequence,
        DateTime timestamp,
        string contractId = EsContractId,
        double price = EsPrice)
    {
        var entityId = new TickDataEntityId(contractId, ValueDate, AssetTypeId.Futures);
        var eventTimestamp = new DateTimeOffset(timestamp);
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
            EventSource = "integration-test",
            ReceivedOn = timestamp,
            Price = new FuturesMarketPriceSnapshot(
                contractId,
                42,
                7,
                AssetTypeId.Futures,
                ValueDate,
                null,
                new FuturesMarketTradeSnapshot(
                    (decimal)price,
                    5,
                    sourceSequence,
                    eventTimestamp,
                    eventTimestamp))
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

    sealed class RealtimeHarness : IAsyncDisposable
    {
        readonly FuturesMarketPriceRealtimeActor _primaryActor;
        readonly FuturesItiSignalRealtimeActor _itiActor;
        readonly HandoffCounter _handoffCounter;

        RealtimeHarness(
            FuturesMarketPriceRealtimeActor primaryActor,
            FuturesItiSignalRealtimeActor itiActor,
            HandoffCounter handoffCounter)
        {
            _primaryActor = primaryActor;
            _itiActor = itiActor;
            _handoffCounter = handoffCounter;
        }

        public int RealtimeProjections => Volatile.Read(ref _handoffCounter.Count);

        public static async Task<RealtimeHarness> StartAsync(
            IActorProducer realtimeProducer,
            IMarketDataApi marketDataApi,
            IDbContextFactory dbFactory)
        {
            var mailbox = Substitute.For<IActorMailbox>();
            var actorProducer = new ForwardingEventProducer(realtimeProducer);
            var supervisor = Substitute.For<IActorSupervisor>();
            supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(mailbox);
            supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(actorProducer);

            var handoffCounter = new HandoffCounter();
            var innerProjector = new FuturesItiSignalRealtimeProjector(
                dbFactory,
                Substitute.For<ILogger<FuturesItiSignalRealtimeProjector>>());
            var projector = new CountingRealtimeProjector(
                innerProjector,
                handoffCounter);

            var primaryActor = new FuturesMarketPriceRealtimeActor(new FuturesMarketPriceRealtimeContext(
                supervisor,
                Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>()));
            var itiActor = new FuturesItiSignalRealtimeActor(new FuturesItiSignalRealtimeContext(
                supervisor,
                projector,
                marketDataApi,
                dbFactory,
                Substitute.For<IStatusConsoleWriter>(),
                Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()));
            await primaryActor.StartAsync(supervisor);
            await itiActor.StartAsync(supervisor);
            return new(primaryActor, itiActor, handoffCounter);
        }

        public async ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event)
        {
            var message = Substitute.For<IActorMessage>();
            message.Subject.Returns(@event.Subject);
            message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>().Returns(@event);
            await _primaryActor.HandleMessageAsync(
                message,
                @event.Subject.ThreadId).ConfigureAwait(false);

            await ((IEventActor<FuturesItiSignalRealtimeActor>)_itiActor)
                .ReceiveAsync(
                    Substitute.For<IEventActorContext<FuturesItiSignalRealtimeActor>>(),
                    @event)
                .ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _itiActor.StopAsync();
            await _primaryActor.StopAsync();
        }

        sealed class CountingRealtimeProjector(
            IRealtimeProjector<FuturesItiSignalRealtimeActor> inner,
            HandoffCounter counter)
            : IRealtimeProjector<FuturesItiSignalRealtimeActor>
        {
            public string ActorName => inner.ActorName;
            public string ProjectorName => inner.ProjectorName;
            public IReadOnlyCollection<Type> ProjectedEventTypes => inner.ProjectedEventTypes;
            public IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
                inner.ProjectionDescriptors;
            public IEventActorContext Context => inner.Context;
            public ILogger Logger => inner.Logger;

            public ValueTask StartAsync(
                IEventActorContext context,
                CancellationToken cancellationToken = default) =>
                inner.StartAsync(context, cancellationToken);

            public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
                inner.StopAsync(cancellationToken);

            public ValueTask<bool> ProcessRealtimeEventAsync(
                IEvent domainEvent,
                CancellationToken cancellationToken = default)
            {
                if (domainEvent is FuturesItiSignalGeneratedEvent)
                    Interlocked.Increment(ref counter.Count);
                return inner.ProcessRealtimeEventAsync(domainEvent, cancellationToken);
            }
        }

        sealed class ForwardingEventProducer(IActorProducer inner) : IActorProducer
        {
            public bool IsRunning => true;

            public ValueTask SendAsync<TCommand, TEntityId>(
                ActorSubject subject,
                TCommand command,
                TEntityId entityId)
                where TCommand : class, ICommand<TEntityId>
                where TEntityId : IActorEntityId =>
                inner.SendAsync(subject, command, entityId);

            public ValueTask SendAsync<TEvent, TEntityId>(
                ActorSubject subject,
                TEvent @event)
                where TEvent : class, IEvent<TEntityId>
                where TEntityId : IActorEntityId =>
                inner.SendAsync<TEvent, TEntityId>(subject, @event);

            public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(
                ActorSubject subject,
                TQuery query)
                where TQuery : class, IQuery<TResult>
                where TResult : class =>
                inner.RequestAsync<TResult, TQuery>(subject, query);

            public ValueTask<ServiceResult<TResult>> RequestAsync<
                TCommand,
                TEntityId,
                TResult>(
                ActorSubject subject,
                TCommand command,
                TEntityId entityId)
                where TCommand : class, ICommand<TEntityId>
                where TEntityId : IActorEntityId
                where TResult : class =>
                inner.RequestAsync<TCommand, TEntityId, TResult>(
                    subject,
                    command,
                    entityId);

            public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<
                TCommand,
                TEntityId,
                TResult>(
                ActorSubject subject,
                TCommand command,
                TEntityId entityId,
                CancellationToken cancellationToken = default)
                where TCommand : class, ICommand<TEntityId>
                where TEntityId : IActorEntityId
                where TResult : class =>
                inner.RequestFunctionAsync<TCommand, TEntityId, TResult>(
                    subject,
                    command,
                    entityId,
                    cancellationToken);

            public ValueTask StartAsync(ActorMailboxId mailboxId) =>
                ValueTask.CompletedTask;

            public ValueTask StopAsync() => ValueTask.CompletedTask;
        }

        sealed class HandoffCounter
        {
            public int Count;
        }

    }

    sealed class ItiEventProbe : IAsyncDisposable
    {
        readonly string _contractId;
        readonly DateOnly _valueDate;
        readonly NatsActorEventListener _listener;
        readonly ConcurrentDictionary<Guid, FuturesItiSignalGeneratedEvent> _generated = new();
        readonly ConcurrentDictionary<Guid, FuturesItiSignalGeneratedCompleteEvent> _completed = new();
        readonly TaskCompletionSource<bool> _firstCycle = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<bool> _secondCycle = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<bool> _longerPeriods = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        ItiEventProbe(
            string contractId,
            DateOnly valueDate,
            NatsActorEventListener listener)
        {
            _contractId = contractId;
            _valueDate = valueDate;
            _listener = listener;
        }

        public static async Task<ItiEventProbe> StartAsync(
            string contractId,
            DateOnly valueDate,
            ActorType actorType = ActorType.Realtime,
            string actorName = FuturesItiSignalRealtimeActor.ActorName)
        {
            var listener = new NatsActorEventListener(
                new NatsEventListenerOptions(),
                Substitute.For<ILogger<NatsActorEventListener>>());
            var probe = new ItiEventProbe(contractId, valueDate, listener);
            await listener.StartAsync(
                $"iti-realtime-pipeline-{Guid.NewGuid():N}",
                new Dictionary<ActorMailboxId, List<string>>
                {
                    [new ActorMailboxId(actorType, actorName)] =
                    [
                        FuturesItiSignalGeneratedEvent.Verb,
                        FuturesItiSignalGeneratedCompleteEvent.Verb,
                        FuturesItiSignalGeneratedFailEvent.Verb
                    ]
                },
                probe.HandleEventAsync);
            return probe;
        }

        public IReadOnlyCollection<FuturesItiSignalGeneratedEvent> GeneratedFor(
            TimeFrameType period) => _generated.Values
                .Where(@event => @event.EntityId.TimePeriod == period)
                .ToArray();

        public Task WaitForCompletionCountAsync(int expectedCount) => expectedCount switch
        {
            3 => _firstCycle.Task.WaitAsync(TimeSpan.FromSeconds(45)),
            6 => _secondCycle.Task.WaitAsync(TimeSpan.FromSeconds(45)),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedCount))
        };

        public Task WaitForLongerPeriodCompletionsAsync() =>
            _longerPeriods.Task.WaitAsync(TimeSpan.FromSeconds(20));

        public async ValueTask DisposeAsync() => await _listener.StopAsync();

        ValueTask HandleEventAsync(string eventVerb, NatsMsg<byte[]> message)
        {
            if (eventVerb == FuturesItiSignalGeneratedEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedEvent>()!;
                if (Matches(@event.EntityId))
                    _generated.TryAdd(EventKey(@event.Id, @event.CommandId), @event);
            }
            else if (eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!;
                if (Matches(@event.EntityId))
                {
                    _completed.TryAdd(EventKey(@event.Id, @event.CommandId), @event);
                    if (_completed.Count >= 3)
                        _firstCycle.TrySetResult(true);
                    if (_completed.Count >= 6)
                        _secondCycle.TrySetResult(true);
                    if (_completed.Values
                        .Where(completed => completed.EntityId.TimePeriod is
                            TimeFrameType.Weekly or TimeFrameType.Monthly)
                        .Select(completed => completed.EntityId.TimePeriod)
                        .Distinct()
                        .Count() == 2)
                    {
                        _longerPeriods.TrySetResult(true);
                    }
                }
            }
            else if (eventVerb == FuturesItiSignalGeneratedFailEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedFailEvent>()!;
                if (Matches(@event.EntityId))
                {
                    var exception = new InvalidOperationException(@event.ErrorMessage);
                    _firstCycle.TrySetException(exception);
                    _secondCycle.TrySetException(exception);
                    _longerPeriods.TrySetException(exception);
                }
            }

            return ValueTask.CompletedTask;
        }

        bool Matches(FuturesItiSignalEntityId entityId) =>
            StringComparer.Ordinal.Equals(entityId.ContractId, _contractId)
            && entityId.ValueDate == _valueDate;

        static Guid EventKey(Guid eventId, Guid commandId) =>
            eventId != Guid.Empty ? eventId : commandId;
    }
}
