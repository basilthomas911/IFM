using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

public static class TdiReceiptFixture
{
    public static readonly DateOnly Date = new(2026, 8, 25);
    public static FuturesRsiSignalsGeneratedEvent Source(int minute = 0) => new()
    {
        Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), AggregateId = "ESU6", EventSource = "tdi-receipt-fixture",
        EntityId = new("ESU6", Date, TimeFrameType.OneMinute, 13), PeriodLength = 13,
        FuturesRsiSignals = Enumerable.Range(0, 34).Select(index => new FuturesRsiSignalReadModel(
            "ESU6", Date, TimeFrameType.OneMinute, 13, new TimeOnly(9, 30).AddMinutes(index + minute),
            5500m + index, 1m, 1m, 0m, 1m, 0.5m, 2d, 40d + index, 0d, 1d, index + 1L + minute * 34L,
            new DateTime(2026, 8, 25, 13, 30, 0, DateTimeKind.Utc).AddMinutes(index + minute)) { IsWarm = true }).ToArray()
    };
    public static FuturesTdiSignalReadModel Prior => new()
    {
        ContractId = "ESU6", ValueDate = Date, TimePeriod = TimeFrameType.OneMinute,
        ConfigurationId = FuturesTdiConfiguration.StandardConfigurationId,
        SchemaVersion = FuturesTdiConfiguration.CurrentSchemaVersion, PriceSignalDivergence = -1d
    };

    public sealed class Scenario
    {
        public readonly FuturesTdiResultReceipts Receipts;
        public readonly FuturesTdiSignalRealtimeState State;
        public readonly FuturesTdiSignalRealtimeProjector Projector;
        public readonly IMarketDataDbContext Db;
        public readonly IDbContextFactory Factory;
        public readonly IEventActorContext<FuturesTdiSignalRealtimeActor> Context;
        public readonly List<FuturesTdiSignalGeneratedEvent> Sources = [];
        public readonly List<FuturesTdiSignalGeneratedCompleteEvent> Completions = [];
        public FuturesTdiSignalReadModel? Persisted = Prior;
        public bool FailSource, FailDatabase, FailCompletion, ThrowAfterSource, ThrowAfterDatabase, ThrowAfterCompletion;
        public int DatabaseWrites;
        public Func<FuturesTdiSignalReadModel, Task>? WriteDatabase;
        public Scenario(FuturesTdiResultReceipts receipts, IEventActorContext<FuturesTdiSignalRealtimeActor>? context = null)
        {
            Receipts = receipts; State = new(receipts);
            Db = Substitute.For<IMarketDataDbContext>();
            Db.GetLastFuturesTdiSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeFrameType>(), Arg.Any<string>())
                .Returns(_ => Task.FromResult(Persisted));
            Db.InsertFuturesTdiSignalAsync(Arg.Any<FuturesTdiSignalReadModel>()).Returns(async call =>
            {
                if (FailDatabase) throw new IOException("Injected TDI database failure.");
                var signal = call.Arg<FuturesTdiSignalReadModel>();
                if (WriteDatabase is not null) await WriteDatabase(signal);
                Persisted = signal; DatabaseWrites++;
                if (ThrowAfterDatabase) throw new IOException("Injected lost database response after commit.");
            });
            Factory = Substitute.For<IDbContextFactory>(); Factory.MarketDataDb.Returns(Db);
            Projector = new(Factory, NullLogger<FuturesTdiSignalRealtimeProjector>.Instance, receipts);
            Context = context ?? Substitute.For<IEventActorContext<FuturesTdiSignalRealtimeActor>>();
            Context.ActorId.Returns(new ActorMailboxId(ActorType.Realtime, FuturesTdiSignalRealtimeActor.ActorName));
            Context.SendAsync<FuturesTdiSignalGeneratedEvent, FuturesTdiSignalEntityId>(Arg.Any<FuturesTdiSignalGeneratedEvent>()).Returns(call =>
            {
                if (FailSource) throw new IOException("Injected source publication refusal.");
                Sources.Add(call.Arg<FuturesTdiSignalGeneratedEvent>());
                if (ThrowAfterSource) throw new IOException("Injected ambiguous source publication.");
                return ValueTask.CompletedTask;
            });
            Context.SendAsync<FuturesTdiSignalGeneratedCompleteEvent, FuturesTdiSignalEntityId>(Arg.Any<FuturesTdiSignalGeneratedCompleteEvent>()).Returns(call =>
            {
                if (FailCompletion) throw new IOException("Injected completion publication refusal.");
                Completions.Add(call.Arg<FuturesTdiSignalGeneratedCompleteEvent>());
                if (ThrowAfterCompletion) throw new IOException("Injected ambiguous completion publication.");
                return ValueTask.CompletedTask;
            });
        }
        public ValueTask Start() => Projector.StartAsync(Context);
        public ValueTask<bool> Process(FuturesRsiSignalsGeneratedEvent source) => State.ProcessAsync(source, source.FuturesRsiSignals, FuturesTdiConfiguration.Standard, Db, Projector);
    }
}
