using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Captures and seals all data used by one Market Condition evaluation.</summary>
public interface IMarketConditionSnapshotProvider
{
    Task<MarketConditionSnapshotCaptureResult> CaptureAsync(
        ExecuteMarketConditionPipelineCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Receives bounded, pre-aggregated latest-value snapshots from market-data adapters.</summary>
public interface IMarketConditionSnapshotCache
{
    long Revision { get; }
    void Upsert(int fundId, string instrumentRoot, TimeFrameType targetHorizon, MarketConditionSnapshot snapshot);
    void Clear();
}

/// <summary>
/// Captures one revision-stable point-in-time view from the process-local adapter cache.
/// Feed, option-chain, calendar, session and health adapters publish bounded aggregates through
/// <see cref="IMarketConditionSnapshotCache"/>; calculation never rereads a source after this capture.
/// </summary>
public sealed class MarketConditionSnapshotProvider : IMarketConditionSnapshotProvider, IMarketConditionSnapshotCache
{
    static readonly ConcurrentDictionary<SnapshotKey, MarketConditionSnapshot> Latest = new();
    static long _revision;

    public long Revision => Interlocked.Read(ref _revision);

    public void Upsert(int fundId, string instrumentRoot, TimeFrameType targetHorizon,
        MarketConditionSnapshot snapshot)
    {
        if (fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        if (!string.Equals(instrumentRoot, "ES", StringComparison.Ordinal))
            throw new ArgumentException("Market Condition V1 supports the ES instrument root.", nameof(instrumentRoot));
        if (targetHorizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentOutOfRangeException(nameof(targetHorizon));
        ArgumentNullException.ThrowIfNull(snapshot);
        Latest[new SnapshotKey(fundId, instrumentRoot, targetHorizon)] = snapshot;
        Interlocked.Increment(ref _revision);
    }

    public void Clear()
    {
        Latest.Clear();
        Interlocked.Increment(ref _revision);
    }

    public Task<MarketConditionSnapshotCaptureResult> CaptureAsync(
        ExecuteMarketConditionPipelineCommand command,
        CancellationToken cancellationToken = default)
        => CaptureAtAsync(command, DateTime.UtcNow, cancellationToken);

    internal Task<MarketConditionSnapshotCaptureResult> CaptureAtAsync(
        ExecuteMarketConditionPipelineCommand command,
        DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = new SnapshotKey(command.FundId, command.InstrumentRoot, command.TargetHorizon);
        var attempts = Math.Max(1, command.ParameterSet.Snapshot.SnapshotCaptureAttempts);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = Revision;
            if (!Latest.TryGetValue(key, out var candidate))
                return Task.FromResult(Failed(command, evaluationTimestampUtc,
                    "No bounded Market Condition source snapshot is available."));
            var captured = Rebind(candidate, command, evaluationTimestampUtc, before);
            if (before != Revision) continue;
            var invalid = FirstInvalidObservation(captured);
            if (invalid is not null)
                return Task.FromResult(Failed(command, evaluationTimestampUtc,
                    $"Required source metadata is invalid for {invalid.SourceId}."));
            return Task.FromResult(new MarketConditionSnapshotCaptureResult
            {
                Outcome = MarketConditionCaptureOutcome.Success,
                Snapshot = MarketConditionSnapshotHash.Seal(captured)
            });
        }
        return Task.FromResult(Failed(command, evaluationTimestampUtc,
            "Market Condition source revisions changed during every bounded capture attempt."));
    }

    static MarketConditionSnapshot Rebind(MarketConditionSnapshot source,
        ExecuteMarketConditionPipelineCommand command, DateTime at, long revision)
        => source with
        {
            SnapshotId = Guid.CreateVersion7(new DateTimeOffset(at, TimeSpan.Zero)),
            SchemaVersion = MarketConditionSnapshot.CurrentSchemaVersion,
            WorkflowId = command.WorkflowId,
            EntityId = command.WorkflowEntityId,
            FundId = command.FundId,
            InstrumentRoot = command.InstrumentRoot,
            TargetHorizon = command.TargetHorizon,
            EvaluationTimestampUtc = at,
            SourceSequenceWatermark = Math.Max(source.SourceSequenceWatermark, revision),
            FuturesQuote = source.FuturesQuote with
            {
                QuoteObservation = At(source.FuturesQuote.QuoteObservation, at),
                TradeObservation = At(source.FuturesQuote.TradeObservation, at)
            },
            OptionChainQuality = source.OptionChainQuality with
                { Observation = At(source.OptionChainQuality.Observation, at) },
            SessionState = source.SessionState with { Observation = At(source.SessionState.Observation, at) },
            EventRiskState = source.EventRiskState with { Observation = At(source.EventRiskState.Observation, at) },
            VolatilityShockState = source.VolatilityShockState with
                { Observation = At(source.VolatilityShockState.Observation, at) },
            OperationalHealth = source.OperationalHealth.Select(x => x with
                { Observation = At(x.Observation, at) }).ToArray(),
            DataQualityItems = source.DataQualityItems.Select(x => At(x, at)).ToArray(),
            SnapshotSha256 = string.Empty
        };

    static MarketSourceObservation At(MarketSourceObservation source, DateTime at) => source with
    {
        AgeSeconds = source.SourceTimestampUtc == default
            ? 0m
            : Math.Max(0m, (decimal)(at - source.SourceTimestampUtc).TotalSeconds)
    };

    static MarketSourceObservation? FirstInvalidObservation(MarketConditionSnapshot snapshot)
        => Observations(snapshot).FirstOrDefault(x =>
            string.IsNullOrWhiteSpace(x.SourceId) ||
            x.Availability == MarketSourceAvailability.Unknown ||
            x.Validity == MarketSourceValidity.Unknown ||
            (x.Availability == MarketSourceAvailability.Available && x.SourceTimestampUtc == default));

    static IEnumerable<MarketSourceObservation> Observations(MarketConditionSnapshot snapshot)
    {
        yield return snapshot.FuturesQuote.QuoteObservation;
        yield return snapshot.FuturesQuote.TradeObservation;
        yield return snapshot.OptionChainQuality.Observation;
        yield return snapshot.SessionState.Observation;
        yield return snapshot.EventRiskState.Observation;
        yield return snapshot.VolatilityShockState.Observation;
        foreach (var item in snapshot.OperationalHealth) yield return item.Observation;
        foreach (var item in snapshot.DataQualityItems) yield return item;
    }

    static MarketConditionSnapshotCaptureResult Failed(
        ExecuteMarketConditionPipelineCommand command, DateTime at, string message)
        => new()
        {
            Outcome = MarketConditionCaptureOutcome.Failed,
            FailureCategory = MarketConditionFailureCategory.RequiredInputInvalid,
            ReasonCode = MarketConditionReasonCodes.RequiredInput,
            SafeMessage = message,
            Snapshot = new MarketConditionSnapshot
            {
                SnapshotId = Guid.CreateVersion7(new DateTimeOffset(at, TimeSpan.Zero)),
                WorkflowId = command.WorkflowId,
                EntityId = command.WorkflowEntityId,
                FundId = command.FundId,
                InstrumentRoot = command.InstrumentRoot,
                TargetHorizon = command.TargetHorizon,
                EvaluationTimestampUtc = at
            }
        };

    readonly record struct SnapshotKey(int FundId, string InstrumentRoot, TimeFrameType TargetHorizon);
}

/// <summary>Creates stable hashes for already-sealed Market Condition snapshots.</summary>
public static class MarketConditionSnapshotHash
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Compute(MarketConditionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var canonical = snapshot with
        {
            SnapshotSha256 = string.Empty,
            OperationalHealth = snapshot.OperationalHealth.OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray(),
            DataQualityItems = snapshot.DataQualityItems.OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical, Options))));
    }

    public static MarketConditionSnapshot Seal(MarketConditionSnapshot snapshot)
    {
        var canonical = snapshot with
        {
            OperationalHealth = snapshot.OperationalHealth.OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray(),
            DataQualityItems = snapshot.DataQualityItems.OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray(),
            SnapshotSha256 = string.Empty
        };
        return canonical with { SnapshotSha256 = Compute(canonical) };
    }
}
