using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Represents the currently active workflow execution for one stable workflow entity.</summary>
public sealed record ActiveIntrinsicTimeStrategyWorkflowReadModel(
    string WorkflowEntityId,
    StrategyWorkflowId WorkflowId,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    TimeFrameType TimePeriod,
    StrategyWorkflowStage CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    ReadOnlyMemory<byte> StatePayload,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc);
