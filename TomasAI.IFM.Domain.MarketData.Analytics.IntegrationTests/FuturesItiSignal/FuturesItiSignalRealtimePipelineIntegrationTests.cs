using System.Collections.Concurrent;
using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

/// <summary>
/// Exercises the complete ITI boundary from a Core NATS market-price update through
/// the realtime actors, durable command actors, event projectors, and Scylla storage.
/// Only the external market-data provider is controlled by the test.
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
    public async Task CoreNatsTrades_ProjectDailyWeeklyAndMonthlySignalsDurably()
    {
        await ResetAsync();
        var marketDataApi = CreateReadyMarketDataApi();
        await using var probe = await ItiEventProbe.StartAsync(EsContractId, ValueDate);
        await using var realtime = await RealtimeHarness.StartAsync(
            _durableProducer,
            marketDataApi);

        try
        {
            await realtime.PublishAsync(CreateEvent(101, FirstTimestamp));
            await probe.WaitForCompletionCountAsync(3);

            await AssertStoredSignalsAsync(expectedCount: 1, FirstTimestamp);
            AssertGeneratedEvents(probe, expectedCount: 1);

            // A distinct live trade is valid input even when its numerical price is unchanged.
            var secondTimestamp = FirstTimestamp.AddSeconds(1);
            await realtime.PublishAsync(CreateEvent(102, secondTimestamp));
            await probe.WaitForCompletionCountAsync(6);

            await AssertStoredSignalsAsync(expectedCount: 2, FirstTimestamp, secondTimestamp);
            AssertGeneratedEvents(probe, expectedCount: 2);
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
    public async Task IneligibleCoreNatsTrade_DoesNotCreateDurableItiSignals(
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
                    .Returns(Task.FromException<decimal>(
                        new FuturesLastPriceUnavailableException(VxContractId)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition));
        }

        await using var realtime = await RealtimeHarness.StartAsync(
            _durableProducer,
            marketDataApi);
        try
        {
            await realtime.PublishAsync(CreateEvent(201, FirstTimestamp, eventContractId));
            await Task.Delay(TimeSpan.FromSeconds(1));

            realtime.DurableHandoffs.Should().Be(0);
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
    public async Task DailyCompletionDerivedCommandIds_AreDurablyIdempotent()
    {
        await ResetAsync();
        await using var probe = await ItiEventProbe.StartAsync(EsContractId, ValueDate);
        var source = CreateDailyCompletion();

        try
        {
            foreach (var period in new[] { TimeFrameType.Weekly, TimeFrameType.Monthly })
            {
                var result = await SendDurableItiCommandAsync(
                    period,
                    FuturesItiSignalGeneratedComplete.CreateDerivedCommandId(source, period));
                result.Success.Should().BeTrue();
            }
            await probe.WaitForLongerPeriodCompletionsAsync();

            // A redelivered Daily completion creates these same deterministic IDs.
            foreach (var period in new[] { TimeFrameType.Weekly, TimeFrameType.Monthly })
            {
                var result = await SendDurableItiCommandAsync(
                    period,
                    FuturesItiSignalGeneratedComplete.CreateDerivedCommandId(source, period));
                result.Success.Should().BeFalse(
                    "the durable actor must reject a repeated derived command ID");
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

    static FuturesItiSignalGeneratedCompleteEvent CreateDailyCompletion()
    {
        var entityId = new FuturesItiSignalEntityId(
            EsContractId,
            ValueDate,
            TimeFrameType.Daily);
        var signal = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = EsContractId,
            ValueDate = ValueDate,
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTime = FirstTimestamp,
            IntrinsicPrice = EsPrice,
            TradingDays = 1
        };
        return new FuturesItiSignalGeneratedCompleteEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesItiSignalGeneratedCompleteEvent.Actor,
                FuturesItiSignalGeneratedCompleteEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            AggregateId = entityId.Format(),
            EventId = 1,
            EventSource = "integration-test",
            ReceivedOn = FirstTimestamp,
            FuturesItiSignal = signal,
            VixFuturesPrice = VxPrice,
            DeriveLongerPeriods = true,
            CreatedOn = FirstTimestamp,
            CreatedBy = "integration-test"
        };
    }

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
        api.GetFuturesPriceAsync(VxContractId).Returns(Task.FromResult((decimal)VxPrice));
        return api;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent(
        long sourceSequence,
        DateTime timestamp,
        string contractId = EsContractId)
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
                    (decimal)EsPrice,
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
        readonly NatsActorConsumer _consumer;
        readonly NatsActorProducer _publisher;
        readonly HandoffCounter _handoffCounter;

        RealtimeHarness(
            FuturesMarketPriceRealtimeActor primaryActor,
            FuturesItiSignalRealtimeActor itiActor,
            NatsActorConsumer consumer,
            NatsActorProducer publisher,
            HandoffCounter handoffCounter)
        {
            _primaryActor = primaryActor;
            _itiActor = itiActor;
            _consumer = consumer;
            _publisher = publisher;
            _handoffCounter = handoffCounter;
        }

        public int DurableHandoffs => Volatile.Read(ref _handoffCounter.Count);

        public static async Task<RealtimeHarness> StartAsync(
            IActorProducer durableProducer,
            IMarketDataApi marketDataApi)
        {
            var queues = Substitute.For<IActorThreadQueues>();
            var mailbox = Substitute.For<IActorMailbox>();
            mailbox.ThreadQueues.Returns(queues);
            var actorProducer = Substitute.For<IActorProducer>();
            actorProducer.StartAsync(Arg.Any<ActorMailboxId>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.CompletedTask);
            actorProducer.StopAsync().Returns(ValueTask.CompletedTask);
            var supervisor = Substitute.For<IActorSupervisor>();
            supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(mailbox);
            supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(actorProducer);

            var handoffCounter = new HandoffCounter();
            var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
            commandApi.GenerateFuturesItiSignalAsync(
                    Arg.Any<string>(),
                    Arg.Any<DateOnly>(),
                    Arg.Any<TimeFrameType>(),
                    Arg.Any<DateTime>(),
                    Arg.Any<double>(),
                    Arg.Any<double>(),
                    Arg.Any<Guid?>())
                .Returns(call => SendDurableCommandAsync(
                    durableProducer,
                    handoffCounter,
                    call.ArgAt<string>(0),
                    call.ArgAt<DateOnly>(1),
                    call.ArgAt<TimeFrameType>(2),
                    call.ArgAt<DateTime>(3),
                    call.ArgAt<double>(4),
                    call.ArgAt<double>(5),
                    call.ArgAt<Guid?>(6)));
            var commandApiFactory = Substitute.For<IActorMarketDataAnalyticsCommandApiFactory>();
            commandApiFactory.Create(Arg.Any<IEventActorContext>()).Returns(commandApi);

            var primaryActor = new FuturesMarketPriceRealtimeActor(
                supervisor,
                Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>());
            var itiActor = new FuturesItiSignalRealtimeActor(
                supervisor,
                commandApiFactory,
                marketDataApi,
                Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());
            supervisor.ActorExists(primaryActor.Id).Returns(true);
            supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>())
                .Returns(ImmutableHashSet.Create(itiActor.Id));
            supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor>
            {
                [primaryActor.Id] = primaryActor,
                [itiActor.Id] = itiActor
            });
            queues.TryAdmitAsync(
                    Arg.Any<IActorMessage>(),
                    Arg.Any<ActorSubject>(),
                    Arg.Any<CancellationToken>())
                .Returns(call => AdmitAsync(primaryActor, itiActor, call));

            var url = Environment.GetEnvironmentVariable("IFM_NATS_URL")
                ?? "nats://localhost:4222";
            var consumer = new NatsActorConsumer(
                new NatsConsumerOptions
                {
                    Url = url,
                    DispatcherCount = 1,
                    DispatcherCapacity = 16,
                    SubscriptionCapacity = 16,
                    FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
                    {
                        [ActorType.Realtime] = CoreNatsTrafficClass.Optional
                    }
                },
                Substitute.For<ILogger>());
            var publisher = new NatsActorProducer(
                new NatsProducerOptions { Url = url },
                Substitute.For<ILogger>());

            await primaryActor.StartAsync(supervisor);
            await itiActor.StartAsync(supervisor);
            await consumer.StartAsync(
                supervisor,
                ActorType.Realtime,
                $"futures-iti-full-pipeline-{Guid.NewGuid():N}");
            await Task.Delay(250);
            return new(primaryActor, itiActor, consumer, publisher, handoffCounter);
        }

        public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event)
            => _publisher.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                @event.Subject,
                @event);

        public async ValueTask DisposeAsync()
        {
            await _publisher.StopAsync();
            await _consumer.StopAsync();
            await _itiActor.StopAsync();
            await _primaryActor.StopAsync();
        }

        static ValueTask<ServiceResult<GuidResult>> SendDurableCommandAsync(
            IActorProducer durableProducer,
            HandoffCounter handoffCounter,
            string contractId,
            DateOnly valueDate,
            TimeFrameType timePeriod,
            DateTime timestamp,
            double futuresPrice,
            double vixFuturesPrice,
            Guid? commandId)
        {
            Interlocked.Increment(ref handoffCounter.Count);
            var entityId = new FuturesItiSignalEntityId(contractId, valueDate, timePeriod);
            var command = new GenerateFuturesItiSignalCommand(
                contractId,
                valueDate,
                timePeriod,
                timestamp,
                futuresPrice,
                vixFuturesPrice)
            {
                CommandId = commandId ?? Guid.NewGuid(),
                Subject = new ActorSubject(
                    ActorType.Command,
                    GenerateFuturesItiSignalCommand.Actor,
                    GenerateFuturesItiSignalCommand.Verb,
                    entityId.Format()),
                EntityId = entityId,
                ErrorCode = GenerateFuturesItiSignalCommand.ErrorId
            };
            return durableProducer.RequestAsync<
                GenerateFuturesItiSignalCommand,
                FuturesItiSignalEntityId,
                GuidResult>(command.Subject, command, entityId);
        }

        static async ValueTask<ActorAdmissionResult> AdmitAsync(
            FuturesMarketPriceRealtimeActor primaryActor,
            FuturesItiSignalRealtimeActor itiActor,
            NSubstitute.Core.CallInfo call)
        {
            var message = call.Arg<IActorMessage>();
            var subject = call.Arg<ActorSubject>();
            if (subject.ActorId == primaryActor.Id)
                await primaryActor.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
            else if (subject.ActorId == itiActor.Id)
                await itiActor.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
            else
                return ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxRetired);
            return ActorAdmissionResult.AcceptedResult;
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
            DateOnly valueDate)
        {
            var listener = new NatsActorEventListener(
                new NatsEventListenerOptions(),
                Substitute.For<ILogger<NatsActorEventListener>>());
            var probe = new ItiEventProbe(contractId, valueDate, listener);
            await listener.StartAsync(
                $"iti-realtime-pipeline-{Guid.NewGuid():N}",
                new Dictionary<ActorMailboxId, List<string>>
                {
                    [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
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
            3 => _firstCycle.Task.WaitAsync(TimeSpan.FromSeconds(20)),
            6 => _secondCycle.Task.WaitAsync(TimeSpan.FromSeconds(20)),
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
