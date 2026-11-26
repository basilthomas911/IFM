using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Runs real bar handlers with a deliberately failing command transport, without live market or database access.</summary>
public static class RealtimeMailboxSafetyFixture
{
    public static readonly DateOnly ValueDate = new(2026, 8, 25);
    static readonly IMarketSessionCalendar Calendar = new CmeFuturesMarketSessionCalendar();
    static DateTimeOffset Start => Calendar.GetSession(ValueDate).StartUtc;
    public static FuturesMarketPriceUpdatedRealtimeEvent Trade(long ordinal) => new()
    {
        Id = new Guid(checked((int)ordinal), 0, 0, new byte[8]),
        Subject = new(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
            FuturesMarketPriceUpdatedRealtimeEvent.Verb, "ESU6-2026-08-25"),
        EntityId = new("ESU6", ValueDate, AssetTypeId.Futures), AggregateId = "ESU6", EventSource = "mailbox-safety-probe",
        ReceivedOn = Start.AddSeconds(ordinal == 1 ? 1 : 17).UtcDateTime,
        UpdateSource = FuturesMarketPriceUpdateSource.Trade,
        Price = new("ESU6", 1, 1, AssetTypeId.Futures, ValueDate, null,
            new FuturesMarketTradeSnapshot(100m + ordinal, 1, ordinal,
                Start.AddSeconds(ordinal == 1 ? 1 : 17), Start.AddSeconds(ordinal == 1 ? 1 : 17),
                NormalizedTradeAction.New, NormalizedTradeSide.Buy, NormalizedTradeConditionFlags.None,
                new Guid("0dd12a24-e00b-4277-bccd-52b52aca2b2d"), ordinal))
    };
    public sealed class Handler
    {
        public readonly List<FuturesTradeSessionBarReadModel> Attempts = [];
        public readonly List<FuturesTradeSessionBarReadModel> Persisted = [];
        public bool Fail, Throw, ThrowAfterCommit;
        public int RejectAfterSuccesses = int.MaxValue;
        public readonly List<Guid> CommandIds = [];
        public IFuturesTradeSessionBarSignalRealtimeContext Context { get; }
        public FuturesTradeSessionBarSignalRealtimeActor Actor { get; }
        public Handler(FuturesTradeSessionBarCheckpointStore? checkpoints = null, List<FuturesTradeSessionBarReadModel>? persisted = null)
        {
            if (persisted is not null) Persisted = persisted;
            Context = Substitute.For<IFuturesTradeSessionBarSignalRealtimeContext>();
            Context.Logger.Returns(NullLogger<FuturesTradeSessionBarSignalRealtimeActor>.Instance);
            Context.ActorId.Returns(new ActorMailboxId(ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName));
            Actor = new(Context);
            var series = MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
            Context.Accumulators.Returns(new FuturesTradeSessionBarAccumulatorRegistry(Calendar,
                new PrefixFuturesTradeSessionBarSeriesResolver(new Dictionary<string, MarketSeriesIdentity>(StringComparer.OrdinalIgnoreCase) { ["ES"] = series }),
                new Clock(Start.AddSeconds(40)), checkpoints));
            Context.Accumulators.Restore();
            Context.RequestAsync<PublishFuturesTradeSessionBarCommand, FuturesTradeSessionBarEntityId>(Arg.Any<PublishFuturesTradeSessionBarCommand>())
                .Returns(call =>
                {
                    var command = call.Arg<PublishFuturesTradeSessionBarCommand>(); Attempts.Add(command.Bar); CommandIds.Add(command.CommandId);
                    if (Throw) throw new IOException("Injected publication transport failure.");
                    if (Fail || Persisted.Count >= RejectAfterSuccesses) return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(-429, "Injected publication rejection."));
                    if (!Persisted.Any(bar => bar.ObservationId == command.Bar.ObservationId)) Persisted.Add(command.Bar);
                    if (ThrowAfterCommit) throw new IOException("Injected lost publication response after commit.");
                    return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
                });
        }
        public ValueTask<bool> Receive(FuturesMarketPriceUpdatedRealtimeEvent source)
            => source.ExecuteAsync(Context, NullLogger.Instance);
        public ValueTask ReceiveBatch(IReadOnlyList<IEvent> inputs, CancellationToken token = default)
        {
            var id = inputs[0] switch
            {
                FuturesMarketPriceUpdatedRealtimeEvent price => Context.Accumulators.Resolve(price.EntityId.ContractId, price.EntityId.ValueDate),
                FuturesTradeSessionBarSignalBarrierRealtimeEvent barrier => barrier.EntityId,
                _ => throw new InvalidDataException("Unsupported batch fixture input.")
            };
            var thread = new ActorThreadId(ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName, id.Format());
            return Actor.HandleBatchAsync(inputs.Select(input => (IActorMessage)new BatchMessage(input, thread)).ToArray(), thread, token);
        }
    }
    public sealed class BatchMessage(IEvent input, ActorThreadId thread) : IActorMessage, IActorDeliveryCompletion
    {
        public ActorSubject Subject => new(thread.ActorType, thread.Name, input.Subject.Verb, thread.EntityId);
        public ActorSubject SourceSubject => input.Subject;
        public ActorSubject ReplySubject { get; set; }
        public int AdmissionSizeBytes => 512;
        public List<bool> Outcomes { get; } = [];
        public int Disposals { get; private set; }
        public Action? OnDisposed { get; set; }
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => input as TEvent;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public void Dispose() { Disposals++; OnDisposed?.Invoke(); }
        public ValueTask CompleteDeliveryAsync(bool succeeded) { Outcomes.Add(succeeded); return ValueTask.CompletedTask; }
    }
    public static async Task<object> Verify()
    {
        var root = Path.Combine(Path.GetTempPath(), "IFM-bar-recovery-" + Guid.NewGuid().ToString("N"));
        var persisted = new List<FuturesTradeSessionBarReadModel>();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var reported = new Handler(store, persisted); await reported.Receive(Trade(1)); reported.Fail = true;
            var failed = false;
            try { await reported.Receive(Trade(2)); } catch (IOException) { failed = true; }
            Require(failed && reported.Attempts.Count == 1 && persisted.Count == 0, "Failed publication must fail delivery.");
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var restarted = new Handler(store, persisted);
            await restarted.Receive(Trade(2)); await restarted.Receive(Trade(2));
            Require(persisted.Count == 1 && restarted.Attempts.Count == 1, "Restart must recover the pending bar exactly once.");
        }
        var openRoot = root + "-open";
        using (var store = new FuturesTradeSessionBarCheckpointStore(openRoot))
        { var first = new Handler(store); await first.Receive(Trade(1)); }
        using (var store = new FuturesTradeSessionBarCheckpointStore(openRoot))
        {
            var restored = new Handler(store); await restored.Receive(Trade(2));
            Require(restored.Persisted.Count == 1 && restored.Persisted[0].Open == 101m
                && restored.Persisted[0].TradeCount == 1, "Acknowledged open bars must survive restart.");
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root + "-ambiguous"))
        {
            var handler = new Handler(store); await handler.Receive(Trade(1)); handler.ThrowAfterCommit = true;
            try { await handler.Receive(Trade(2)); } catch (IOException) { }
            handler.ThrowAfterCommit = false; await handler.Receive(Trade(2));
            Require(handler.Persisted.Count == 1 && handler.Attempts.Count == 2
                && handler.CommandIds.Distinct().Count() == 1, "Ambiguous commit must reuse its command identity.");
        }
        var thrown = new Handler(); await thrown.Receive(Trade(1)); thrown.Throw = true;
        var escaped = false;
        try { await thrown.Receive(Trade(2)); } catch (IOException) { escaped = true; }
        thrown.Throw = false; await thrown.Receive(Trade(2));
        Require(escaped && thrown.Attempts.Count == 2 && thrown.Persisted.Count == 1, "Thrown publication must remain replayable.");
        return new { recoveryDirectory = root, failedPublicationFailsDelivery = true, pendingPublicationSurvivesRestart = true,
            acknowledgedOpenBarSurvivesRestart = true, redeliveryIsIdempotent = true, ambiguousCommitKeepsCommandIdentity = true };
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
