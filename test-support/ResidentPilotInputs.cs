using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport.ResidentActors;

/// <summary>Five isolated actor paths; no financial execution or broker command is created.</summary>
public enum ResidentPilotKind { Vwap, MarketOutlook, FuturesPlan, VerticalSpreadPlan, IronCondorPlan }

/// <summary>Deterministic coherent protocol inputs for actor tests and paired benchmarks.</summary>
public sealed class ResidentPilotInputs
{
    public DateTime Epoch { get; } = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);
    public DateOnly Date => DateOnly.FromDateTime(Epoch);
    public DateTime At(long revision) => Epoch.AddMilliseconds(revision);

    public ICommand Create(ResidentPilotKind kind, int key, long revision)
    {
        var at = At(revision);
        var contract = "ES" + new DateOnly(2026, 12, 1).AddDays(key).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        if (kind == ResidentPilotKind.MarketOutlook)
            return new InsertMarketOutlookSnapshotCommand(new MarketOutlookReadModel
            {
                ContractId = contract, ValueDate = Date, UpdatedAtUtc = at, MarketDataAsOfUtc = at,
                FuturesEodData = new FuturesEodDataV2ReadModel
                {
                    ContractId = contract, Symbol = "ES", ValueDate = Date,
                    OpenPrice = 5000, LowPrice = 4900, HighPrice = 5100, ClosePrice = 5000 + revision % 100 / 4m, Volume = revision
                }
            });
        if (kind == ResidentPilotKind.Vwap)
        {
            var config = FuturesVwapConfiguration.Standard;
            var id = new FuturesVwapSignalEntityId(contract, Date, config.ConfigurationId);
            return new UpdateFuturesVwapSignalCommand
            {
                CommandId = Guid.NewGuid(), EntityId = id,
                Subject = new(ActorType.Command, UpdateFuturesVwapSignalCommand.Actor, UpdateFuturesVwapSignalCommand.Verb, id.Format()),
                Configuration = config, SessionStartUtc = new(Epoch.AddHours(-1)), SessionEndUtc = new(Epoch.AddHours(8)),
                SourceCheckpoint = new FuturesVwapSourceCheckpoint
                {
                    StreamEpochId = new Guid(key + 1, 0, 0, new byte[8]), RecoveryGenerationId = new Guid(key + 1, 1, 1, new byte[8]),
                    LastTradeOrdinal = revision, LastTradeSourceSequence = revision, AsOfUtc = new(at),
                    CumulativePriceVolume = 5000m * revision, CumulativeVolume = revision,
                    EligibleTradeCount = revision, LastPrice = 5000, IsValid = true, IsReplayComplete = true
                }
            };
        }
        var strategy = kind switch
        {
            ResidentPilotKind.FuturesPlan => TradeStrategyKind.FuturesOutright,
            ResidentPilotKind.VerticalSpreadPlan => TradeStrategyKind.VerticalSpread,
            _ => TradeStrategyKind.IronCondor
        };
        var position = Position(strategy, key, revision, at);
        return kind switch
        {
            ResidentPilotKind.FuturesPlan => Futures(position, at),
            ResidentPilotKind.VerticalSpreadPlan => Vertical(position, at),
            _ => Condor(position, at)
        };
    }

    static UpdateFuturesTradePlanCommand Futures(StrategyPositionSnapshot position, DateTime at)
    {
        var id = new FuturesTradePlanId(position.Id, position.ValueDate);
        return new() { CommandId = Guid.NewGuid(), EntityId = id, Position = position, SourceEventId = Guid.NewGuid(),
            RequestedAtUtc = at, Subject = new(ActorType.Function, UpdateFuturesTradePlanCommand.Actor,
                UpdateFuturesTradePlanCommand.Verb, id.Format()) };
    }

    static UpdateVerticalSpreadTradePlanCommand Vertical(StrategyPositionSnapshot position, DateTime at)
    {
        var id = new VerticalSpreadTradePlanId(position.Id, position.ValueDate);
        return new() { CommandId = Guid.NewGuid(), EntityId = id, Position = position, SourceEventId = Guid.NewGuid(),
            RequestedAtUtc = at, Subject = new(ActorType.Function, UpdateVerticalSpreadTradePlanCommand.Actor,
                UpdateVerticalSpreadTradePlanCommand.Verb, id.Format()) };
    }

    static UpdateIronCondorTradePlanCommand Condor(StrategyPositionSnapshot position, DateTime at)
    {
        var id = new TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId(position.Id, position.ValueDate);
        return new() { CommandId = Guid.NewGuid(), EntityId = id, Position = position, SourceEventId = Guid.NewGuid(),
            RequestedAtUtc = at, Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor,
                UpdateIronCondorTradePlanCommand.Verb, id.Format()) };
    }

    static StrategyPositionSnapshot Position(TradeStrategyKind strategy, int key, long revision, DateTime at)
    {
        var legs = Enumerable.Range(0, strategy == TradeStrategyKind.FuturesOutright ? 1 : strategy == TradeStrategyKind.VerticalSpread ? 2 : 4)
            .Select(index => new StrategyPositionLeg
            {
                TradeLegId = new Guid(index + 1, 0, 0, new byte[8]), ContractId = "SYNTHETIC-" + index,
                AssetFamily = strategy == TradeStrategyKind.FuturesOutright ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption,
                PutCall = (byte)(index < 2 ? 1 : 2), SignedQuantity = index % 2 == 0 ? 1 : -1,
                OpeningPrice = 10 + index, CurrentPrice = 10.25m + index,
                LastSourceSequence = revision, LastPriceAtUtc = at
            }).ToArray();
        return new()
        {
            Id = StrategyPositionId.Create(new TradeEntityId(1, 2, 3, key + 10000), strategy), StrategyKind = strategy,
            ValueDate = DateOnly.FromDateTime(at), PositionSequence = revision, RouteGeneration = 1, IsOpen = true,
            Phase = StrategyPositionPhase.MarkToMarket, AsOfUtc = at, Legs = legs,
            DailyPnlPoints = revision / 100m, UnrealizedPnl = revision / 100m,
            MarketValue = legs.Sum(static leg => leg.SignedQuantity * leg.CurrentPrice)
        };
    }
}

/// <summary>A materialized actor boundary; PostgreSQL benchmarks deliberately exclude NATS wire latency.</summary>
public sealed class ResidentPilotMessage(ICommand request) : IActorMessage
{
    public ServiceResult? Result { get; private set; }
    public object? TypedResult { get; private set; }
    public Func<ValueTask>? BeforeReply { get; init; }
    public ActorSubject Subject => request.Subject;
    public ActorSubject ReplySubject { get; set; } = default!;
    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => request as TCommand;
    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
    public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
    public async ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
    {
        if (BeforeReply is not null) await BeforeReply().ConfigureAwait(false);
        TypedResult = result;
        Result = result as ServiceResult;
    }
    public void ReleasePayload() { }
    public NatsMsg<byte[]> GetMessage() => default;
    public void Dispose() { }
}

/// <summary>Controlled fixture clock; deadlines remain enforced by the real Function lifecycle.</summary>
public sealed class ResidentPilotClock(DateTime now) : TimeProvider
{
    public DateTime Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => new(Now);
}
