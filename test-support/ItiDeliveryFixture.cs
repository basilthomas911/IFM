using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Real ITI ingress, scheduler and C# computation; command transport/persistence deliberately substituted.</summary>
public sealed class ItiDeliveryFixture : IAsyncDisposable
{
    public const string Contract = "ES-ITI-DELIVERY";
    public static readonly DateOnly Date = new(2026, 8, 25);
    public static readonly DateTime At = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc);
    public IFuturesItiSignalRealtimeContext Context { get; }
    public readonly Effects Service;
    public readonly FuturesItiSignalRealtimeActor Actor;
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Mailbox;
    public decimal Vx = 20m;
    readonly IActorSupervisor supervisor;
    public ItiDeliveryFixture(FuturesItiIngressRecovery? recovery = null, Effects? effects = null)
    {
        Service = effects ?? new(); SeedAtr(8);
        Context = Substitute.For<IFuturesItiSignalRealtimeContext>();
        Context.IngressRecovery.Returns(recovery);
        Context.Logger.Returns(NullLogger<FuturesItiSignalRealtimeActor>.Instance);
        Context.Telemetry.Returns(new FuturesItiSignalRuntimeTelemetry(TimeProvider.System));
        Context.HealthEvidence.Returns(new LivePipelineEvidence(TimeProvider.System));
        var api = Substitute.For<IMarketDataApi>(); Context.MarketDataApi.Returns(api);
        api.TryGetOnTheRunFuturesContract(Arg.Any<string>(), out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { var root = call.ArgAt<string>(0); call[1] = new FuturesContractV3ReadModel(
                root == "ES" ? Contract : "VX-ITI-DELIVERY", root, root, root, "FUT", "USD", "CME", "50", new(2026, 12, 18), true); return true; });
        api.TryGetLastTickPrice("VX-ITI-DELIVERY", out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call => { var source = Source(1); call[1] = source.Price with { Trade = source.Price.Trade!.Value with { LastPrice = Vx } }; return true; });
        Context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(call => ValueTask.FromResult(Service.Accept(call.Arg<GenerateFuturesItiSignalCommand>())));
        supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16));
        container.Resolve<IActorSupervisor>().Returns(supervisor); supervisor.Container.Returns(container); Context.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        Pool = new(supervisor, NullLogger.Instance); Pool.Initialize(1); supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>())
            .Returns(call => Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var id = new ActorMailboxId(ActorType.Realtime, FuturesItiSignalRealtimeActor.ActorName); Context.ActorId.Returns(id);
        Mailbox = new(supervisor, id); supervisor.CreateMailbox(id).Returns(Mailbox); Actor = new(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = Actor });
    }
    public static FuturesMarketPriceUpdatedRealtimeEvent Source(long sequence, decimal price = 100m)
    {
        var source = RealtimeMailboxSafetyFixture.Trade(sequence);
        var trade = source.Price.Trade!.Value with { LastPrice = price, EventTimestamp = new DateTimeOffset(At.AddTicks(sequence)),
            ReceiveTimestamp = new DateTimeOffset(At.AddTicks(sequence + 1)) };
        return source with { SourceDataset = "GLBX.MDP3", EntityId = new(Contract, Date, AssetTypeId.Futures),
            Price = source.Price with { ContractId = Contract, Trade = trade } };
    }
    public static void SeedAtr(double value) => RegimeDiscoverySignalCacheAdapter.Publish(new FuturesAtrSignalReadModel
    {
        ContractId = Contract, ValueDate = Date, TimePeriod = TimeFrameType.FiveMinutes, PeriodLength = 14,
        AtrValue = value, IsWarm = true,
        Metadata = new() { SignalKey = new(MarketSeriesIdentity.ForContract(Contract), MarketAnalyticsSignalKind.Atr, TimeFrameType.FiveMinutes, "atr-test"), ContractId = Contract, ValueDate = Date, MarketDataAsOfUtc = new DateTimeOffset(At.AddMinutes(-5)),
            CalculatedAtUtc = new DateTimeOffset(At.AddMinutes(-5)), SourceSequence = 123, IsValid = true, CalculationVersion = "test" }
    });
    public ValueTask Start() => Actor.StartAsync(supervisor);
    public ValueTask<bool> Receive(FuturesMarketPriceUpdatedRealtimeEvent source) => source.ExecuteAsync(Context);
    public async Task<bool> Deliver(FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        var message = new IndicatorDeliveryFixture.Message(source, "Iti");
        if (!await Mailbox.ThreadQueues.WriteAsync(message)) throw new IOException("Fixture admission refused.");
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); return await message.Settled.Task;
    }
    public void ResetMeasurement() { Service.Attempts.Clear(); Context.ClearReceivedCalls(); }
    public async ValueTask DisposeAsync() { await Actor.StopAsync(); await Pool.DisposeAsync(); }
    public sealed class Effects
    {
        public bool Fail, Throw, ThrowAfterCommit, NullReply;
        public readonly List<GenerateFuturesItiSignalCommand> Attempts = [], Committed = [];
        public FuturesItiSignalCommandState State { get; } = new();
        public int Changes;
        public Action<GenerateFuturesItiSignalCommand>? WriteReceipt;
        public ServiceResult<GuidResult> Accept(GenerateFuturesItiSignalCommand command)
        {
            Attempts.Add(command);
            if (Throw) throw new IOException("Injected ITI transport failure.");
            if (NullReply) return null!;
            if (Fail) return new ServiceFailed<GuidResult>(-429, "Injected required ITI command refusal.");
            if (!Committed.Any(previous => previous.CommandId == command.CommandId)) Apply(command);
            else
            {
                var prior = State.FuturesItiSignal;
                var retried = command.Execute(State);
                if (!retried.Success || !ReferenceEquals(prior, State.FuturesItiSignal))
                    throw new InvalidDataException("Retry changed the real ITI computation twice.");
            }
            WriteReceipt?.Invoke(command);
            if (ThrowAfterCommit) throw new IOException("Injected lost ITI response after commit.");
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }
        public void Apply(GenerateFuturesItiSignalCommand command)
        {
            // The real command actor performs this freeze for legacy commands, but must preserve already frozen inputs.
            if (!command.InputsFrozen) command = command with { RequireDailyAtr = true, DailyAtr = new(Contract, 8, At.AddMinutes(-5), At.AddMinutes(-5), 123) };
            var before = State.FuturesItiSignal;
            var result = command.Execute(State);
            if (!result.Success) throw new InvalidDataException("C# ITI computation failed.");
            if (!ReferenceEquals(before, State.FuturesItiSignal)) Changes++;
            Committed.Add(command);
        }
    }
}
