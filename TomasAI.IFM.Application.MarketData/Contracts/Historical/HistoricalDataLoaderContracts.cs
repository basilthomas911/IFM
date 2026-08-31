using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Identifies the durable data load lifecycle.</summary>
public enum HistoricalDataLoaderStatus : byte
{
    /// <summary>The attempt has not started.</summary>
    None,
    /// <summary>The attempt is running or resumable.</summary>
    Processing,
    /// <summary>The attempt completed and its manifest is immutable.</summary>
    Completed,
    /// <summary>The attempt failed at its recorded checkpoint.</summary>
    Failed
}

/// <summary>Describes one expected trading date missing from normalized history.</summary>
public sealed record HistoricalDataLoaderGap(DateOnly ValueDate, string SeriesKey, string ReasonCode);

/// <summary>Describes one actual-contract transition in a continuation series.</summary>
public sealed record HistoricalDataLoaderRoll(
    string SeriesKey,
    DateOnly ValueDate,
    string PreviousContractId,
    string CurrentContractId);

/// <summary>Captures deterministic coverage, gap, and roll audit results.</summary>
public sealed record HistoricalDataLoaderAudit(
    int ValidSessionCount,
    IReadOnlyList<HistoricalDataLoaderGap> Gaps,
    IReadOnlyList<HistoricalDataLoaderRoll> Rolls);

/// <summary>Provides the durable command/query state for one data load attempt.</summary>
public sealed record HistoricalDataLoaderState
{
    /// <summary>Gets the attempt identity.</summary>
    public required Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the stable request hash used for repeat-run ownership.</summary>
    public required string RequestSha256 { get; init; }
    /// <summary>Gets the lifecycle status.</summary>
    public HistoricalDataLoaderStatus Status { get; init; }
    /// <summary>Gets the last durable acquisition checkpoint.</summary>
    public required HistoricalAcquisitionCheckpoint Checkpoint { get; init; }
    /// <summary>Gets the immutable manifest after completion.</summary>
    public MarketDataHistoricalManifest? Manifest { get; init; }
    /// <summary>Gets the coverage audit after completion.</summary>
    public HistoricalDataLoaderAudit? Audit { get; init; }
    /// <summary>Gets the terminal failure text without provider data.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the last durable update time.</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>Persists data load checkpoints and immutable manifests in PostgreSQL.</summary>
public interface IHistoricalDataLoaderStore
{
    /// <summary>Gets an attempt by its stable identity.</summary>
    ValueTask<HistoricalDataLoaderState?> GetAsync(Guid attemptId, CancellationToken cancellationToken);
    /// <summary>Gets an already-completed attempt for a request hash.</summary>
    ValueTask<HistoricalDataLoaderState?> GetCompletedByRequestHashAsync(
        string requestSha256,
        CancellationToken cancellationToken);
    /// <summary>Creates or advances an attempt atomically.</summary>
    ValueTask SaveAsync(HistoricalDataLoaderState state, CancellationToken cancellationToken);
}

/// <summary>Persists immutable raw observations with insert-if-absent semantics.</summary>
public interface IHistoricalObservationStore
{
    /// <summary>Writes one normalized observation if its deterministic identity is new.</summary>
    ValueTask<bool> TryWriteObservationAsync(
        FuturesTradeSessionBarReadModel observation,
        CancellationToken cancellationToken);
    /// <summary>Writes one raw Daily EOD session if its deterministic identity is new.</summary>
    ValueTask<bool> TryWriteRawEodAsync(
        FuturesEodObservationReadModel observation,
        CancellationToken cancellationToken);
    /// <summary>Gets one exact raw EOD session.</summary>
    ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken);
    /// <summary>Gets valid or invalid raw Daily EOD sessions in ascending value-date order.</summary>
    async ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
            throw new ArgumentOutOfRangeException(nameof(startDate));
        List<FuturesEodObservationReadModel> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
            if (await GetRawEodAsync(seriesIdentity, date, cancellationToken).ConfigureAwait(false) is { } value)
                values.Add(value);
        return values;
    }
}

/// <summary>Publishes bounded private replay batches to analytics realtime workers.</summary>
public interface IHistoricalReplayPublisher
{
    /// <summary>Publishes one already-normalized bounded batch.</summary>
    ValueTask PublishAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken);
}

/// <summary>Provides a no-op replay target for raw-data-load-only processes.</summary>
public sealed class NullHistoricalReplayPublisher : IHistoricalReplayPublisher
{
    /// <inheritdoc />
    public ValueTask PublishAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Publishes ordered, completed Daily observations to bar-derived Analytics actors.</summary>
public interface IHistoricalDailyReplayPublisher
{
    /// <summary>Publishes a complete ordered replay window.</summary>
    ValueTask PublishAsync(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        DateOnly targetValueDate,
        CancellationToken cancellationToken);
}

/// <summary>Provides a no-op Daily replay target outside Analytics hosts.</summary>
public sealed class NullHistoricalDailyReplayPublisher : IHistoricalDailyReplayPublisher
{
    /// <inheritdoc />
    public ValueTask PublishAsync(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        DateOnly targetValueDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observations);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
