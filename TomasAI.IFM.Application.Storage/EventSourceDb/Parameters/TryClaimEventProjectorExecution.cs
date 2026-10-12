using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TryClaimEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    DateTime leaseExpiresAtUtc,
    DateTime nowUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        TimestampTz(leaseExpiresAtUtc),
        TimestampTz(nowUtc),
        Text(updatedTimestamp));
}
