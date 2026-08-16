using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>
/// Actor-owned hot ITI state. Each stream is hydrated once from durable storage,
/// then every market tick is evaluated without another storage read.
/// </summary>
public sealed class FuturesItiSignalRealtimeState(IDbContextFactory dbFactory)
{
    static readonly TimeFrameType[] Periods =
    [
        TimeFrameType.Daily,
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    ];

    readonly IDbContextFactory _dbFactory = dbFactory
        ?? throw new ArgumentNullException(nameof(dbFactory));
    readonly Dictionary<StreamKey, StreamState> _streams = [];

    internal async ValueTask<IReadOnlyList<FuturesItiSignalEvaluation>> EvaluateAsync(
        string contractId,
        DateOnly valueDate,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice)
    {
        List<FuturesItiSignalEvaluation> evaluations = [];
        foreach (var period in Periods)
        {
            var bucketStart = GetCalendarBucketStart(valueDate, period);
            var key = new StreamKey(contractId, period, bucketStart);
            if (!_streams.TryGetValue(key, out var state))
            {
                state = await HydrateAsync(key, valueDate).ConfigureAwait(false);
                _streams.Add(key, state);
            }

            state.Observe(valueDate, timestamp, futuresPrice);
            var command = new GenerateFuturesItiSignalCommand(
                contractId,
                valueDate,
                period,
                timestamp,
                futuresPrice,
                vixFuturesPrice,
                state.TimeFrameStartValueDate);
            if (FuturesItiSignalCompute.TryCompute(
                    command,
                    state.LastDurableSignal,
                    out var signal))
            {
                evaluations.Add(new FuturesItiSignalEvaluation(key, command, signal));
            }
        }

        return evaluations;
    }

    internal void Confirm(FuturesItiSignalEvaluation evaluation)
    {
        if (_streams.TryGetValue(evaluation.Key, out var state))
            state.LastDurableSignal = evaluation.Signal;
    }

    async ValueTask<StreamState> HydrateAsync(StreamKey key, DateOnly valueDate)
    {
        var projected = await _dbFactory.MarketDataDb
            .GetFuturesItiTimeFrameStateAsync(
                key.ContractId,
                key.TimePeriod,
                key.CalendarBucketStart)
            .ConfigureAwait(false);
        if (projected is not null)
        {
            var projectedFrameStart = projected.TimeFrameStartValueDate == default
                ? projected.ValueDate
                : projected.TimeFrameStartValueDate;
            return new StreamState(projectedFrameStart, projected);
        }

        // Legacy fallback: find the first observed row in the current calendar
        // bucket. New writes use the exact versioned state projection above.
        var rows = await _dbFactory.MarketDataDb.GetFuturesItiSignalsForContractAsync(
            key.ContractId,
            key.CalendarBucketStart,
            valueDate).ConfigureAwait(false);
        var periodRows = (rows ?? [])
            .Where(row => row.TimePeriod == key.TimePeriod
                && GetCalendarBucketStart(row.ValueDate, key.TimePeriod)
                    == key.CalendarBucketStart)
            .OrderBy(row => row.SequenceId)
            .ThenBy(row => row.IntrinsicTime)
            .ToArray();

        if (periodRows.Length == 0)
            return new StreamState(valueDate, null);

        var latest = periodRows[^1];
        var explicitFrameStart = latest.TimeFrameStartValueDate;
        var frameStart = explicitFrameStart != default
            && GetCalendarBucketStart(explicitFrameStart, key.TimePeriod)
                == key.CalendarBucketStart
                ? explicitFrameStart
                : periodRows.Min(row => row.ValueDate);
        latest = latest with { TimeFrameStartValueDate = frameStart };
        return new StreamState(frameStart, latest);
    }

    internal static DateOnly GetCalendarBucketStart(
        DateOnly valueDate,
        TimeFrameType period)
        => period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(
                -(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => throw new ArgumentOutOfRangeException(
                nameof(period),
                $"Unsupported ITI time period: {period}")
        };

    internal readonly record struct StreamKey(
        string ContractId,
        TimeFrameType TimePeriod,
        DateOnly CalendarBucketStart);

    internal sealed class StreamState(
        DateOnly timeFrameStartValueDate,
        FuturesItiSignalV2ReadModel? lastDurableSignal)
    {
        internal DateOnly TimeFrameStartValueDate { get; } = timeFrameStartValueDate;
        internal FuturesItiSignalV2ReadModel? LastDurableSignal { get; set; } = lastDurableSignal;
        internal DateOnly LatestObservedValueDate { get; private set; }
        internal DateTime LatestObservedTimestamp { get; private set; }
        internal double LatestObservedPrice { get; private set; }

        internal void Observe(DateOnly valueDate, DateTime timestamp, double price)
        {
            LatestObservedValueDate = valueDate;
            LatestObservedTimestamp = timestamp;
            LatestObservedPrice = price;
        }
    }
}

internal sealed record FuturesItiSignalEvaluation(
    FuturesItiSignalRealtimeState.StreamKey Key,
    GenerateFuturesItiSignalCommand Command,
    FuturesItiSignalV2ReadModel Signal);
