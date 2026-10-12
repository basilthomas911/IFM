using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertIntrinsicTimeStrategyWorkflowStartAttempt(
    string WorkflowEntityId,
    DateTime RequestedAtUtc,
    Guid RequestedWorkflowId,
    string Decision,
    Guid? ActiveWorkflowId,
    Guid StartCommandId,
    Guid TriggerEventId,
    string ActiveStage,
    string ReasonCode,
    long SourceEventId) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowEntityId, RequestedAtUtc, RequestedWorkflowId, Decision, ActiveWorkflowId,
        StartCommandId, TriggerEventId, ActiveStage, ReasonCode, SourceEventId
    };
}
