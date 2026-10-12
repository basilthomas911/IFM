using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertIntrinsicTimeStrategyWorkflowByStatusDay(
    string Status,
    DateOnly StartedDate,
    DateTime StartedAtUtc,
    Guid WorkflowId,
    string WorkflowEntityId,
    string Outcome,
    string CurrentStage,
    long WorkflowRevision,
    DateTime? TerminalAtUtc,
    string StopReasonCode) : IBindValue
{
    public object Bind() => new object?[]
    {
        Status, StartedDate, StartedAtUtc, WorkflowId, WorkflowEntityId, Outcome,
        CurrentStage, WorkflowRevision, TerminalAtUtc, StopReasonCode
    };
}
