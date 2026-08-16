using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the MACD (Moving Average Convergence Divergence) signal
/// for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesMacdDailySignalQuery : IQuery<FuturesMacdSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesMacdSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesMacdDailySignal";
    [IgnoreMember] public const int ErrorId = 1022;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; }

    [Key(3)]
    public TimeFrameType TimePeriod { get; init; }

    [Key(4)]
    public int SignalEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSignalEmaPeriod;

    [Key(5)]
    public int FastEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalFastEmaPeriod;

    [Key(6)]
    public int SlowEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSlowEmaPeriod;

    [IgnoreMember]
    public int PeriodLength => SignalEmaPeriod;

    public GetFuturesMacdDailySignalQuery() { }

    public GetFuturesMacdDailySignalQuery(
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod)
    {
        ContractId = contractId ?? string.Empty;
        TimePeriod = timePeriod;
        SignalEmaPeriod = signalEmaPeriod;
        FastEmaPeriod = fastEmaPeriod;
        SlowEmaPeriod = slowEmaPeriod;
        EntityId = new FuturesMacdDailySignalEntityId(
            contractId,
            timePeriod,
            signalEmaPeriod,
            fastEmaPeriod,
            slowEmaPeriod);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesMacdDailySignalQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod)
    {
        Subject = subject;
        ContractId = contractId ?? string.Empty;
        TimePeriod = timePeriod;
        SignalEmaPeriod = signalEmaPeriod;
        FastEmaPeriod = fastEmaPeriod;
        SlowEmaPeriod = slowEmaPeriod;
        EntityId = new FuturesMacdDailySignalEntityId(
            contractId,
            timePeriod,
            signalEmaPeriod,
            fastEmaPeriod,
            slowEmaPeriod);
        ErrorCode = ErrorId;
    }
}

