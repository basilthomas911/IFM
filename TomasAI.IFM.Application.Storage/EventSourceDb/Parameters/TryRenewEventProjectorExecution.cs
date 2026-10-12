using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TryRenewEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    DateTime nowUtc,
    DateTime leaseExpiresAtUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        Bigint(expectedRevision),
        TimestampTz(nowUtc),
        TimestampTz(leaseExpiresAtUtc),
        Text(updatedTimestamp));
}
