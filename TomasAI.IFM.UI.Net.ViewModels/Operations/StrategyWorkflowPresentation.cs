using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>Semantic color-independent state for one visible pipeline actor indicator.</summary>
public enum PipelineActorDisplayState
{
    Processing,
    Continued,
    Stopped
}

/// <summary>Presentation state for one pipeline actor that has started.</summary>
public sealed record PipelineActorIndicator(
    StrategyWorkflowStage Stage,
    string ShortLabel,
    string ActorName,
    PipelineActorDisplayState DisplayState,
    string AccessibleStatus);

/// <summary>One immutable row in the Strategy workflow list.</summary>
public sealed record StrategyWorkflowRow(
    StrategyWorkflowId WorkflowId,
    IntrinsicTimeStrategyWorkflowEntityId EntityId,
    long WorkflowRevision,
    DateTime OccurredOn,
    TimeFrameType TimePeriod,
    IntrinsicTimeModeType SignalEvent,
    IntrinsicTimeTrendType Trend,
    double FuturesPrice,
    string TriggerStableIdentity,
    WorkflowStrategyMachineStatus MachineStatus,
    StrategyWorkflowOutcome Outcome,
    string EndState,
    IReadOnlyList<PipelineActorIndicator> PipelineActors);

internal static class StrategyWorkflowPresentation
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    static readonly (StrategyWorkflowStage Stage, string ShortLabel, string Name)[] Stages =
    [
        (StrategyWorkflowStage.RegimeDiscovery, "RD", "Regime Discovery"),
        (StrategyWorkflowStage.MarketCondition, "MC", "Market Condition"),
        (StrategyWorkflowStage.TradeSelection, "TS", "Trade Selection"),
        (StrategyWorkflowStage.OrderComposition, "OC", "Order Composition"),
        (StrategyWorkflowStage.RiskManagement, "RM", "Risk Management")
    ];

    public static StrategyWorkflowRow CreateRow(IntrinsicTimeStrategyWorkflowView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var signal = view.TriggerEvent.FuturesItiSignal ?? new FuturesItiSignalV2ReadModel
        {
            ContractId = view.EntityId.ItiSignalEntityId.ContractId,
            TimePeriod = view.EntityId.ItiSignalEntityId.TimePeriod,
            TimeFrameStartValueDate = view.EntityId.ItiSignalEntityId.TimeFrameStartValueDate
        };
        var indicators = Stages
            .Select(stage => CreateIndicator(stage, StageState(view, stage.Stage)))
            .Where(static indicator => indicator is not null)
            .Cast<PipelineActorIndicator>()
            .ToArray();

        return new StrategyWorkflowRow(
            view.WorkflowId,
            view.EntityId,
            view.WorkflowRevision,
            view.TriggerEvent.CreatedOn == default ? view.StartedAtUtc : view.TriggerEvent.CreatedOn,
            view.EntityId.ItiSignalEntityId.TimePeriod,
            signal.IntrinsicTimeMode,
            signal.IntrinsicTimeTrend,
            signal.IntrinsicPrice,
            SignalIdentity(signal),
            view.Status,
            view.Outcome,
            EndState(view),
            indicators);
    }

    public static string RenderDetails(IntrinsicTimeStrategyWorkflowView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder(16 * 1024);
        Append(text, "WORKFLOW", new Dictionary<string, object?>
        {
            ["Workflow ID"] = view.WorkflowId.Value,
            ["Workflow entity"] = view.EntityId.Format(),
            ["Trigger event ID"] = view.TriggerEventId,
            ["Correlation ID"] = view.CorrelationId,
            ["Causation ID"] = view.CausationId,
            ["Definition"] = view.EntityId.WorkflowDefinitionId,
            ["Definition version"] = view.WorkflowDefinitionVersion,
            ["Revision"] = view.WorkflowRevision,
            ["Machine status"] = view.Status,
            ["Business outcome"] = view.Outcome,
            ["Current stage"] = view.CurrentStage,
            ["Stop reason"] = EmptyAsNone(view.StopReasonCode),
            ["Started UTC"] = view.StartedAtUtc,
            ["Updated UTC"] = view.UpdatedAtUtc,
            ["Expires UTC"] = view.ExpiresAtUtc,
            ["Terminal UTC"] = view.TerminalAtUtc
        });

        Append(text, "FUTURES ITI SIGNAL EVENT", new Dictionary<string, object?>
        {
            ["Created UTC"] = view.TriggerEvent.CreatedOn,
            ["Event ID"] = view.TriggerEvent.Id,
            ["Command ID"] = view.TriggerEvent.CommandId,
            ["Entity"] = view.TriggerEvent.EntityId?.Format(),
            ["Event source"] = view.TriggerEvent.EventSource,
            ["Created by"] = view.TriggerEvent.CreatedBy,
            ["VIX futures price"] = view.TriggerEvent.VixFuturesPrice
        });
        if (view.TriggerEvent.FuturesItiSignal is { } signal)
        {
            text.AppendLine("Signal:");
            text.AppendLine(Serialize(signal));
            text.AppendLine();
        }

        foreach (var stage in Stages)
            AppendStage(text, stage.Name, stage.Stage, StageState(view, stage.Stage));

        return text.ToString();
    }

    static PipelineActorIndicator? CreateIndicator(
        (StrategyWorkflowStage Stage, string ShortLabel, string Name) definition,
        StrategyWorkflowStageState state)
    {
        if (state.ProcessingStatus == StrategyActorProcessingStatus.NotStarted)
            return null;

        var display = state.ProcessingStatus switch
        {
            StrategyActorProcessingStatus.Processing => PipelineActorDisplayState.Processing,
            StrategyActorProcessingStatus.Completed when
                state.ContinuationDecision == StrategyWorkflowContinuationDecision.Proceed
                => PipelineActorDisplayState.Continued,
            StrategyActorProcessingStatus.Completed when
                state.ContinuationDecision == StrategyWorkflowContinuationDecision.Stop
                => PipelineActorDisplayState.Stopped,
            StrategyActorProcessingStatus.Completed => PipelineActorDisplayState.Processing,
            _ => PipelineActorDisplayState.Stopped
        };
        var status = $"{definition.Name}: {state.ProcessingStatus}; continuation {state.ContinuationDecision}";
        if (state.CompletedAtUtc is { } completed)
            status += $"; completed {completed:O}";
        if (state.FailedAtUtc is { } failed)
            status += $"; failed {failed:O}";
        if (state.Failure is { } failure)
            status += $"; {failure.ErrorType} {failure.ErrorCode}: {failure.ErrorMessage}";

        return new PipelineActorIndicator(
            definition.Stage,
            definition.ShortLabel,
            definition.Name,
            display,
            status);
    }

    static string EndState(IntrinsicTimeStrategyWorkflowView view)
    {
        if (view.Status == WorkflowStrategyMachineStatus.Started)
            return "In Progress";
        return view.Outcome switch
        {
            StrategyWorkflowOutcome.Completed => "Approved",
            StrategyWorkflowOutcome.NoTrade => "No Trade",
            StrategyWorkflowOutcome.PipelineFailed => "Pipeline Failed",
            StrategyWorkflowOutcome.InvalidResult => "Invalid Result",
            StrategyWorkflowOutcome.TimedOut => "Timed Out",
            StrategyWorkflowOutcome.Cancelled => "Cancelled",
            StrategyWorkflowOutcome.ConsistencyFault => "Consistency Fault",
            _ => "Unknown"
        };
    }

    static StrategyWorkflowStageState StageState(
        IntrinsicTimeStrategyWorkflowView view,
        StrategyWorkflowStage stage)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => view.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => view.MarketCondition,
            StrategyWorkflowStage.TradeSelection => view.TradeSelection,
            StrategyWorkflowStage.OrderComposition => view.OrderComposition,
            StrategyWorkflowStage.RiskManagement => view.RiskManagement,
            _ => new StrategyWorkflowStageState()
        };

    static void AppendStage(
        StringBuilder text,
        string heading,
        StrategyWorkflowStage stage,
        StrategyWorkflowStageState state)
    {
        Append(text, $"{heading.ToUpperInvariant()} RESULT", new Dictionary<string, object?>
        {
            ["Processing status"] = state.ProcessingStatus,
            ["Continuation decision"] = state.ContinuationDecision,
            ["Continuation reasons"] = state.ContinuationReasonCodes.Length == 0
                ? "None"
                : string.Join(", ", state.ContinuationReasonCodes),
            ["Started UTC"] = state.StartedAtUtc,
            ["Completed UTC"] = state.CompletedAtUtc,
            ["Failed UTC"] = state.FailedAtUtc,
            ["Input workflow revision"] = state.InputWorkflowRevision,
            ["Parameter set ID"] = state.ParameterSetId,
            ["Parameter set version"] = state.ParameterSetVersion,
            ["Parameter payload SHA-256"] = EmptyAsNone(state.ParameterPayloadSha256),
            ["Source event ID"] = state.SourceEventId,
            ["Expires UTC"] = state.ExpiresAtUtc
        });

        if (state.Failure is { } failure)
        {
            text.AppendLine("Failure:");
            text.AppendLine(Serialize(failure));
            text.AppendLine();
        }

        if (state is not { Result: { } envelope })
        {
            text.AppendLine("None — workflow did not reach this pipeline result yet.");
            text.AppendLine();
            return;
        }

        Append(text, "RESULT ENVELOPE", new Dictionary<string, object?>
        {
            ["Result ID"] = envelope.ResultId,
            ["Result type"] = envelope.ResultType,
            ["Schema version"] = envelope.SchemaVersion,
            ["Content type"] = envelope.ContentType,
            ["Payload SHA-256"] = envelope.PayloadSha256,
            ["Content size"] = envelope.ContentSize,
            ["Market data as-of UTC"] = envelope.MarketDataAsOfUtc,
            ["Produced UTC"] = envelope.ProducedAtUtc
        });

        object? result = stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => envelope.RegimeResult,
            StrategyWorkflowStage.MarketCondition => envelope.AssessmentResult,
            StrategyWorkflowStage.TradeSelection => envelope.SelectionResult,
            StrategyWorkflowStage.OrderComposition => envelope.CompositionResult,
            StrategyWorkflowStage.RiskManagement => envelope.RiskResult,
            _ => null
        };
        if (result is null)
        {
            text.AppendLine($"Result details unavailable. Opaque payload bytes: {envelope.Payload.Length}.");
            text.AppendLine();
            return;
        }

        text.AppendLine("Result:");
        text.AppendLine(Serialize(result));
        text.AppendLine();
    }

    static void Append(StringBuilder text, string heading, IReadOnlyDictionary<string, object?> values)
    {
        text.AppendLine($"=== {heading} ===");
        foreach (var pair in values)
            text.Append(pair.Key).Append(": ").AppendLine(Format(pair.Value));
        text.AppendLine();
    }

    static string Serialize(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException)
        {
            return $"Result details unavailable: {exception.Message}";
        }
    }

    static string Format(object? value) => value switch
    {
        null => "None",
        DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        string text => EmptyAsNone(text),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "None"
    };

    static string EmptyAsNone(string? value)
        => string.IsNullOrWhiteSpace(value) ? "None" : value;

    static string SignalIdentity(FuturesItiSignalV2ReadModel signal)
        => string.Join(
            '|',
            signal.ContractId,
            signal.ValueDate,
            signal.TimePeriod,
            signal.IntrinsicTime.Ticks,
            signal.IntrinsicTimeMode,
            signal.IntrinsicTimeTrend,
            signal.IntrinsicPrice,
            signal.IntrinsicTimeGroupId,
            signal.IntrinsicTimeLength,
            signal.TrendPrice,
            signal.TrendExtreme,
            signal.TrendReversal,
            signal.TrendDelta,
            signal.TargetDelta,
            signal.Threshold,
            signal.UpTrendTrigger,
            signal.DownTrendTrigger,
            signal.BandLevel,
            signal.ReversalLevel,
            signal.TradeState);
}
