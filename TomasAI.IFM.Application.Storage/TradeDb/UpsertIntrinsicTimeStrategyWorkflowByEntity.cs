using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertIntrinsicTimeStrategyWorkflowByEntity(
    string WorkflowEntityId,
    DateTime StartedAtUtc,
    Guid WorkflowId,
    string Status,
    string Outcome,
    string CurrentStage,
    long WorkflowRevision,
    DateTime? TerminalAtUtc,
    string StopReasonCode) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowEntityId, StartedAtUtc, WorkflowId, Status, Outcome, CurrentStage,
        WorkflowRevision, TerminalAtUtc, StopReasonCode
    };
}
