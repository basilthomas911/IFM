using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRegimeDiscovery(
    Guid WorkflowId,
    string WorkflowEntityId,
    long InputWorkflowRevision,
    Guid CommandId,
    Guid SourceEventId,
    long SourceEventSequence,
    string Status,
    string ParameterPayloadSha256,
    Guid SignalSnapshotId,
    byte[] ResultPayload,
    string ResultPayloadSha256,
    int FailureCode,
    string FailureMessage,
    byte[] ReasonsPayload,
    int SchemaVersion,
    DateTime TerminalAtUtc,
    DateTime UpdatedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
    {
        WorkflowId, WorkflowEntityId, InputWorkflowRevision, CommandId, SourceEventId,
        SourceEventSequence, Status, ParameterPayloadSha256, SignalSnapshotId, ResultPayload,
        ResultPayloadSha256, FailureCode, FailureMessage, ReasonsPayload, SchemaVersion,
        TerminalAtUtc, UpdatedAtUtc
    };
}
