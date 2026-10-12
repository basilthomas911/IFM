using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TryReleaseEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    string expectedStage,
    DateTime nowUtc,
    int retryCount,
    DateTime nextAttemptAtUtc,
    DateTime lastErrorAtUtc,
    string errorMessage,
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
        Integer(retryCount),
        TimestampTz(nextAttemptAtUtc),
        TimestampTz(lastErrorAtUtc),
        Text(errorMessage),
        TimestampTz(updatedAtUtc),
        Text(updatedTimestamp));
}
