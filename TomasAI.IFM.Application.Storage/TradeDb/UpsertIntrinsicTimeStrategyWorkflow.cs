using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertIntrinsicTimeStrategyWorkflow(
    Guid WorkflowId,
    string WorkflowEntityId,
    string WorkflowDefinitionId,
    int WorkflowDefinitionVersion,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    string TimePeriod,
    Guid TriggerEventId,
    Guid CorrelationId,
    string Status,
    string Outcome,
    string CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    byte[] StatePayload,
    string StopReasonCode,
    DateTime StartedAtUtc,
    DateTime? TerminalAtUtc,
    DateTime UpdatedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowId, WorkflowEntityId, WorkflowDefinitionId, WorkflowDefinitionVersion,
        ContractId, TimeFrameStartValueDate, TimePeriod, TriggerEventId, CorrelationId,
        Status, Outcome, CurrentStage, WorkflowRevision, LastEventId, StateSchemaVersion,
        StatePayload, StopReasonCode, StartedAtUtc, TerminalAtUtc, UpdatedAtUtc
    };
}
