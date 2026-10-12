using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertActiveIntrinsicTimeStrategyWorkflow(
    string WorkflowEntityId,
    Guid WorkflowId,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    string TimePeriod,
    string CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    byte[] StatePayload,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowEntityId, WorkflowId, ContractId, TimeFrameStartValueDate, TimePeriod,
        CurrentStage, WorkflowRevision, LastEventId, StateSchemaVersion, StatePayload,
        StartedAtUtc, UpdatedAtUtc
    };
}
