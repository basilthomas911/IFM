using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertIntrinsicTimeStrategyWorkflowTimeline(
    Guid WorkflowId,
    long EventId,
    string WorkflowEntityId,
    long WorkflowRevision,
    string Stage,
    string EventName,
    int EventSchemaVersion,
    byte[] EventPayload,
    DateTime OccurredAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowId, EventId, WorkflowEntityId, WorkflowRevision, Stage, EventName,
        EventSchemaVersion, EventPayload, OccurredAtUtc
    };
}
