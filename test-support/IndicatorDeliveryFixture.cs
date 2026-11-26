using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.TestSupport;

/// <summary>Real indicator actors and scheduler; the downstream command service is deliberately substituted.</summary>
public sealed class IndicatorDeliveryFixture : IAsyncDisposable
{
    public static readonly string[] Kinds = ["Adx", "Atr", "Ema", "Macd", "Rsi", "Vwap", "VxTermStructure"];
    public readonly State Effects;
    public IActor Actor { get; }
    public ActorThreadPoolV2 Pool { get; }
    public ActorMailbox Mailbox { get; }
    public string Kind { get; }
    public bool CatalogAvailable = true;
    readonly IActorSupervisor supervisor;
    IEventActorContext commandContext = null!;
    readonly Action cleanup;
    public IndicatorDeliveryFixture(string kind, State? effects = null,
        Func<string, IEventActorContext, IActor>? factory = null)
    {
        Kind = kind; Effects = effects ?? new();
        supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16));
        supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>())
            .Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var id = new ActorMailboxId(ActorType.Realtime, "Futures" + kind + "Signal");
        Mailbox = new(supervisor, id); supervisor.CreateMailbox(id).Returns(Mailbox);
        cleanup = () => { };
        switch (kind)
        {
            case "Adx":
            {
                var context = Substitute.For<IFuturesAdxSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesAdxSignalRealtimeActor>.Instance);
                Configure(context, id);
                FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear();
                FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Attach(new(Contract, Date, TimeFrameType.FiveMinutes, 14));
                cleanup = FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear;
                Bind<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(context, command => command.Observation!.LastSourceSequence);
                Actor = factory?.Invoke(kind, context) ?? new FuturesAdxSignalRealtimeActor(context); break;
            }
            case "Atr":
            {
                var context = Substitute.For<IFuturesAtrSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesAtrSignalRealtimeActor>.Instance);
                Configure(context, id);
                FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Clear();
                FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Attach(new(Contract, Date, TimeFrameType.FiveMinutes, 14));
                cleanup = FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Clear;
                Bind<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(context, command => command.Observation!.LastSourceSequence);
                Actor = factory?.Invoke(kind, context) ?? new FuturesAtrSignalRealtimeActor(context); break;
            }
            case "Ema":
            {
                var context = Substitute.For<IFuturesEmaSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesEmaSignalRealtimeActor>.Instance);
                Configure(context, id);
                Bind<GenerateFuturesEmaSignalCommand, FuturesTradeSessionBarEntityId>(context, command => command.Observation!.LastSourceSequence);
                Actor = factory?.Invoke(kind, context) ?? new FuturesEmaSignalRealtimeActor(context); break;
            }
            case "Macd":
            {
                var context = Substitute.For<IFuturesMacdSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesMacdSignalRealtimeActor>.Instance);
                Configure(context, id);
                FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Clear();
                FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Attach(new(Contract, Date, TimeFrameType.FiveMinutes, 9, 12, 26));
                cleanup = FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Clear;
                Bind<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(context, command => command.Observation!.LastSourceSequence);
                Actor = factory?.Invoke(kind, context) ?? new FuturesMacdSignalRealtimeActor(context); break;
            }
            case "Rsi":
            {
                var context = Substitute.For<IFuturesRsiSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesRsiSignalRealtimeActor>.Instance);
                Configure(context, id);
                FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Clear();
                FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(new(Contract, Date, TimeFrameType.FiveMinutes, 14));
                cleanup = FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Clear;
                Bind<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(context, command => command.Observation!.LastSourceSequence);
                Actor = factory?.Invoke(kind, context) ?? new FuturesRsiSignalRealtimeActor(context); break;
            }
            case "Vwap":
            {
                var context = Substitute.For<IFuturesVwapSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesVwapSignalRealtimeActor>.Instance);
                Configure(context, id);
                var api = Substitute.For<IMarketDataApi>(); context.MarketDataApi.Returns(api);
                context.SessionCalendar.Returns(new CmeFuturesMarketSessionCalendar());
                api.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>())
                    .Returns(call => { call[1] = ContractModel(Contract, "ES", new(2026, 9, 18)); return CatalogAvailable; });
                Bind<UpdateFuturesVwapSignalCommand, FuturesVwapSignalEntityId>(context, command => command.SourceCheckpoint!.LastTradeOrdinal);
                Actor = factory?.Invoke(kind, context) ?? new FuturesVwapSignalRealtimeActor(context); break;
            }
            case "VxTermStructure":
            {
                var context = Substitute.For<IFuturesVxTermStructureSignalRealtimeContext>();
                context.Logger.Returns(NullLogger<FuturesVxTermStructureSignalRealtimeActor>.Instance);
                Configure(context, id);
                var api = Substitute.For<IMarketDataApi>(); context.MarketDataApi.Returns(api);
                api.TryGetFuturesTermStructureContracts("VX", out Arg.Any<FuturesTermStructureContracts>())
                    .Returns(call => { call[1] = new FuturesTermStructureContracts(
                        ContractModel(Contract, "VX", new(2026, 9, 16)), ContractModel("VXV6", "VX", new(2026, 10, 21))); return CatalogAvailable; });
                Bind<UpdateFuturesVxTermStructureSignalCommand, FuturesVxTermStructureSignalEntityId>(context, command => command.Observation.SourceSequence);
                Actor = factory?.Invoke(kind, context) ?? new FuturesVxTermStructureSignalRealtimeActor(context); break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    void Configure<T>(IRealtimeActorContext<T> context, ActorMailboxId id) where T : IActor
    { commandContext = context; context.ActorId.Returns(id); }
    void Bind<T, TId>(IEventActorContext context, Func<T, long> sequence)
        where T : class, ICommand<TId> where TId : IActorEntityId
        => context.RequestAsync<T, TId>(Arg.Any<T>()).Returns(call =>
            ValueTask.FromResult(Effects.Accept(sequence(call.Arg<T>()))));
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public void ResetMeasurement() { Effects.Attempts.Clear(); commandContext.ClearReceivedCalls(); }
    public Message Input(long sequence, bool applicable = true)
    {
        IEvent source;
        if (Kind is "Vwap" or "VxTermStructure")
        {
            var trade = RealtimeMailboxSafetyFixture.Trade(sequence);
            source = trade with { VwapCheckpoint = new()
            {
                StreamEpochId = trade.Price.Trade!.Value.StreamEpochId, RecoveryGenerationId = Guid.Empty,
                LastTradeOrdinal = sequence, LastTradeSourceSequence = sequence,
                AsOfUtc = trade.Price.Trade!.Value.EventTimestamp,
                CumulativePriceVolume = sequence * 100m, CumulativeVolume = sequence,
                EligibleTradeCount = sequence, LastPrice = 100m, IsValid = true, IsReplayComplete = applicable
            }, UpdateSource = applicable ? FuturesMarketPriceUpdateSource.Trade : FuturesMarketPriceUpdateSource.Quote };
        }
        else
        {
            var start = new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero).AddMinutes(sequence * 5);
            var series = MarketSeriesIdentity.ForContract(Contract);
            var entity = new FuturesTradeSessionBarEntityId(series, TimeFrameType.FiveMinutes);
            source = new FuturesTradeSessionBarClosedRealtimeEvent
            {
                Id = new Guid((int)sequence, 0, 0, new byte[8]),
                EntityId = entity, AggregateId = entity.Format(), EventSource = "indicator-settlement-test",
                Subject = new(ActorType.Realtime, FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                    FuturesTradeSessionBarClosedRealtimeEvent.Verb, entity.Format()),
                ReceivedOn = start.AddMinutes(5).UtcDateTime,
                Observation = new()
                {
                    MarketSeriesIdentity = series,
                    ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FiveMinutes, start.AddMinutes(5), sequence),
                    ContractId = Contract, ValueDate = Date, TimeFrame = TimeFrameType.FiveMinutes,
                    IntervalStartUtc = start, IntervalEndUtc = start.AddMinutes(5),
                    Open = 100m, High = 110m, Low = 95m, Close = 105m, Volume = 10m, TradeCount = 1,
                    PriceVolumeSum = 1050m, FirstSourceSequence = sequence, LastSourceSequence = sequence,
                    FirstMarketEventUtc = start, LastMarketEventUtc = start.AddMinutes(5).AddSeconds(-1),
                    CalculatedAtUtc = start.AddMinutes(5), CalculationVersion = "test-v1",
                    IsComplete = true, IsValid = applicable, CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
                }
            };
        }
        return new(source, Kind);
    }
    public async Task<bool> Deliver(Message message)
    {
        if (!await Mailbox.ThreadQueues.WriteAsync(message)) throw new IOException("Fixture admission refused.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return await message.Settled.Task;
    }
    public async ValueTask DisposeAsync()
    { await Actor.StopAsync(); await Pool.DisposeAsync(); cleanup(); }
    const string Contract = "ESU6";
    static readonly DateOnly Date = new(2026, 8, 25);
    static FuturesContractV3ReadModel ContractModel(string id, string root, DateOnly expiry)
        => new(id, id, root, id, "FUT", "USD", root == "VX" ? "CFE" : "CME", "50", expiry, true);
    public sealed class State
    {
        public bool Fail, Throw, ThrowAfterCommit;
        public Action<long>? WriteCommandReceipt;
        public readonly List<long> Attempts = [], Committed = [];
        public ServiceResult<GuidResult> Accept(long sequence)
        {
            Attempts.Add(sequence);
            if (Throw) throw new IOException("Injected command transport failure.");
            if (Fail) return new ServiceFailed<GuidResult>(-429, "Injected required command refusal.");
            WriteCommandReceipt?.Invoke(sequence);
            if (!Committed.Contains(sequence)) Committed.Add(sequence);
            if (ThrowAfterCommit) throw new IOException("Injected lost reply after commit.");
            return new ServiceOk<GuidResult>(new GuidResult(Guid.Empty));
        }
    }
    public sealed class Message(IEvent source, string kind) : IActorMessage, IActorDeliveryCompletion
    {
        public IEvent Source => source;
        public ActorSubject Subject => new(ActorType.Realtime, (kind == "Iti" ? "FuturesItiSignalRealtime" : "Futures" + kind + "Signal"), source.Subject.Verb, "ordered-test");
        public ActorSubject ReplySubject { get; set; }
        public readonly TaskCompletionSource<bool> Settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Settlements, Disposals;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => source as TEvent;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public ValueTask CompleteDeliveryAsync(bool succeeded) { Settlements++; Settled.TrySetResult(succeeded); return ValueTask.CompletedTask; }
        public void ReleasePayload() { }
        public void Dispose() { Disposals++; Disposed.TrySetResult(); }
    }
}
