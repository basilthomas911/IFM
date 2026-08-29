using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;

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
    readonly IMarketConditionSnapshotAdapterCoordinator? _coordinator;
    readonly TimeProvider _timeProvider;

    public MarketConditionSnapshotProvider(
        IMarketConditionSnapshotAdapterCoordinator? coordinator = null,
        TimeProvider? timeProvider = null)
    {
        _coordinator = coordinator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

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
        => CaptureAtAsync(command, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    internal async Task<MarketConditionSnapshotCaptureResult> CaptureAtAsync(
        ExecuteMarketConditionPipelineCommand command,
        DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken = default)
    {
        using var activity = MarketConditionTelemetry.Start("market-condition.snapshot-capture");
        ArgumentNullException.ThrowIfNull(command);
        var key = new SnapshotKey(command.FundId, command.InstrumentRoot, command.TargetHorizon);
        var attempts = Math.Max(1, command.ParameterSet.Snapshot.SnapshotCaptureAttempts);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = Revision;
            if (!Latest.TryGetValue(key, out var candidate))
            {
                if (_coordinator is null)
                    return Failed(command, evaluationTimestampUtc,
                        "No bounded Market Condition source snapshot is available.");
                try
                {
                    candidate = await _coordinator.PublishAsync(command, Eligibility(command),
                        evaluationTimestampUtc, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (MarketConditionCalculationException error)
                {
                    return Failed(command, evaluationTimestampUtc, error.Message,
                        error.Category, error.ReasonCode);
                }
                catch (Exception)
                {
                    return Failed(command, evaluationTimestampUtc,
                        "An authoritative Market Condition source could not be captured.");
                }
            }
            var captured = Rebind(candidate, command, evaluationTimestampUtc, before);
            if (before != Revision) continue;
            var invalid = FirstInvalidObservation(captured, command.ParameterSet.Snapshot.FutureClockSkewSeconds);
            if (invalid is not null)
                return Failed(command, evaluationTimestampUtc,
                    $"Required source metadata is invalid for {invalid.SourceId}.");
            foreach (var observation in Observations(captured))
                MarketConditionTelemetry.RecordSourceAge(SourceCategory(observation.SourceId),
                    observation.AgeSeconds, command.TargetHorizon);
            return new MarketConditionSnapshotCaptureResult
            {
                Outcome = MarketConditionCaptureOutcome.Success,
                Snapshot = MarketConditionSnapshotHash.Seal(captured)
            };
        }
        return Failed(command, evaluationTimestampUtc,
            "Market Condition source revisions changed during every bounded capture attempt.");
    }

    static MarketConditionWorkflowEligibilityState Eligibility(ExecuteMarketConditionPipelineCommand command)
        => new()
        {
            EntriesEnabled = true,
            RegimeProducedAtUtc = command.WorkflowView.RegimeDiscovery.CompletedAtUtc ?? default,
            TriggerProducedAtUtc = command.TriggerEvent.CreatedOn != default
                ? command.TriggerEvent.CreatedOn : command.TriggerEvent.ReceivedOn
        };

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

    static MarketSourceObservation? FirstInvalidObservation(MarketConditionSnapshot snapshot, int futureClockSkewSeconds)
        => Observations(snapshot).FirstOrDefault(x =>
            string.IsNullOrWhiteSpace(x.SourceId) ||
            x.Availability == MarketSourceAvailability.Unknown ||
            x.Validity == MarketSourceValidity.Unknown ||
            x.Validity == MarketSourceValidity.Invalid ||
            x.SequenceId < 0 ||
            (x.Availability != MarketSourceAvailability.Unavailable && x.SourceTimestampUtc == default) ||
            (x.SourceTimestampUtc != default &&
             x.SourceTimestampUtc > snapshot.EvaluationTimestampUtc.AddSeconds(futureClockSkewSeconds)));

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

    static string SourceCategory(string sourceId)
    {
        if (sourceId.Contains("Option", StringComparison.OrdinalIgnoreCase)) return "option";
        if (sourceId.Contains("Session", StringComparison.OrdinalIgnoreCase)) return "session";
        if (sourceId.Contains("Event", StringComparison.OrdinalIgnoreCase)) return "event";
        if (sourceId.Contains("Volatility", StringComparison.OrdinalIgnoreCase)) return "volatility";
        if (sourceId.Contains("Health", StringComparison.OrdinalIgnoreCase) ||
            sourceId.Contains("Feed", StringComparison.OrdinalIgnoreCase) ||
            sourceId.Contains("Ibkr", StringComparison.OrdinalIgnoreCase) ||
            sourceId.Contains("Cache", StringComparison.OrdinalIgnoreCase)) return "health";
        return "futures";
    }

    static MarketConditionSnapshotCaptureResult Failed(
        ExecuteMarketConditionPipelineCommand command, DateTime at, string message,
        MarketConditionFailureCategory category = MarketConditionFailureCategory.RequiredInputInvalid,
        string reasonCode = MarketConditionReasonCodes.RequiredInput)
        => new()
        {
            Outcome = MarketConditionCaptureOutcome.Failed,
            FailureCategory = category,
            ReasonCode = reasonCode,
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
