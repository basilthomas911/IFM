using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TryTransitionEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    string expectedStage,
    DateTime nowUtc,
    string nextStage,
    string outcome,
    string lastCompletedStage,
    int retryCount,
    DateTime? nextAttemptAtUtc,
    DateTime? lastErrorAtUtc,
    string errorMessage,
    string blockedReason,
    DateTime updatedAtUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        Bigint(expectedRevision),
        Text(expectedStage),
        TimestampTz(nowUtc),
        Text(nextStage),
        Text(outcome),
        Text(lastCompletedStage),
        Integer(retryCount),
        TimestampTz(nextAttemptAtUtc),
        TimestampTz(lastErrorAtUtc),
        Text(errorMessage),
        Text(blockedReason),
        TimestampTz(updatedAtUtc),
        Text(updatedTimestamp));
}
