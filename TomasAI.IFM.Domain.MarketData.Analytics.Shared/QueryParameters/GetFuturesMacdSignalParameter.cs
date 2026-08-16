using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a futures MACD signal for a specific contract and value date.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesMacdSignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TimeFrameType TimePeriod { get; init; }
    [Key(3)] public int SignalEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSignalEmaPeriod;
    [Key(4)] public int FastEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalFastEmaPeriod;
    [Key(5)] public int SlowEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSlowEmaPeriod;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesMacdSignalParameter() { }

    [SerializationConstructor]
    public GetFuturesMacdSignalParameter(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        SignalEmaPeriod = signalEmaPeriod;
        FastEmaPeriod = fastEmaPeriod;
        SlowEmaPeriod = slowEmaPeriod;

        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&timePeriod={TimePeriod}&signalEmaPeriod={SignalEmaPeriod}&fastEmaPeriod={FastEmaPeriod}&slowEmaPeriod={SlowEmaPeriod}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}.{SignalEmaPeriod}.{FastEmaPeriod}.{SlowEmaPeriod}";
}
