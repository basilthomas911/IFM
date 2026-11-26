using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Identity;

namespace TomasAI.IFM.TestSupport;

/// <summary>Production buffered projector and disk journal. Database and publication transports are substituted.</summary>
public sealed class BufferedEodFixture : IAsyncDisposable
{
    public readonly string Directory;
    public readonly FuturesEodDataRealtimeProjector Projector;
    public readonly IEventActorContext Context = Substitute.For<IEventActorContext>();
    public readonly IMarketDataDbContext Storage = Substitute.For<IMarketDataDbContext>();
    public readonly IDbContextFactory Factory = Substitute.For<IDbContextFactory>();
    public readonly ISequenceIdGenerator Sequences = Substitute.For<ISequenceIdGenerator>();
    public readonly IConfiguration Configuration = Substitute.For<IConfiguration>();
    public readonly List<IEvent> Sent = [];
    public readonly List<long> HistoryIds = [];
    public readonly List<Exception> Errors = [];
    public Func<IEvent, ValueTask>? Send;
    public Func<IReadOnlyList<BufferedFuturesEodRow>, Task>? Persist;
    long nextSequence = 41;
    public BufferedEodFixture(string? directory = null)
    {
        Directory = directory ?? Path.Combine(Path.GetTempPath(), "IFM-buffered-eod-" + Guid.NewGuid().ToString("N"));
        Configuration["MarketData:FuturesEodMinuteBatchWriter"].Returns("true");
        Configuration["MarketData:FuturesEodHistorySpoolPath"].Returns(Directory);
        Factory.MarketDataDb.Returns(Storage);
        Sequences.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId).Returns(_ => Interlocked.Increment(ref nextSequence));
        Context.ActorId.Returns(new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName));
        Context.SendAsync<FuturesEodDataInsertedEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedEvent>()).Returns(call => Publish(call.Arg<FuturesEodDataInsertedEvent>()));
        Context.SendAsync<FuturesEodSessionStatisticsUpdatedEvent, FuturesEodDataId>(Arg.Any<FuturesEodSessionStatisticsUpdatedEvent>()).Returns(call => Publish(call.Arg<FuturesEodSessionStatisticsUpdatedEvent>()));
        Context.SendAsync<VixFuturesEodDataInsertedEvent, FuturesEodDataId>(Arg.Any<VixFuturesEodDataInsertedEvent>()).Returns(call => Publish(call.Arg<VixFuturesEodDataInsertedEvent>()));
        Context.SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>()).Returns(call => Publish(call.Arg<FuturesEodDataInsertedCompleteEvent>()));
        Context.SendAsync<VixFuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<VixFuturesEodDataInsertedCompleteEvent>()).Returns(call => Publish(call.Arg<VixFuturesEodDataInsertedCompleteEvent>()));
        Storage.PersistRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>()).Returns(call => Apply(call.Arg<IReadOnlyList<BufferedFuturesEodRow>>()));
        Projector = new(Factory, Sequences, new RecoveryLogger(Errors), Configuration);
    }
    async ValueTask Publish(IEvent source)
    {
        lock (Sent) Sent.Add(source);
        if (Send is not null) await Send(source);
    }
    async Task Apply(IReadOnlyList<BufferedFuturesEodRow> rows)
    {
        foreach (var row in rows) if (row.HistorySequenceId is { } id) HistoryIds.Add(id);
        if (Persist is not null) await Persist(rows);
    }
    public ValueTask Start() => Projector.StartAsync(Context);
    public static FuturesEodDataInsertedEvent Source(int sequence, string contract = "ES-BUFFERED-PROOF") => new()
    {
        Id = new(sequence, 0, 0, new byte[8]), CommandId = new(sequence, 0, 0, new byte[8]),
        EntityId = new(contract, new(2026, 8, 25)),
        Subject = new(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName, FuturesEodDataInsertedEvent.Verb, contract),
        FuturesEodData = new() { ContractId = contract, ValueDate = new(2026, 8, 25), Symbol = "ES", OpenPrice = 100, HighPrice = 150, LowPrice = 90, ClosePrice = 100 + sequence, Volume = sequence }
    };
    public async ValueTask DisposeAsync() => await Projector.DisposeAsync();
    sealed class RecoveryLogger(List<Exception> errors) : ILogger<FuturesEodDataRealtimeProjector>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (exception is not null) lock (errors) errors.Add(exception); }
    }
}
