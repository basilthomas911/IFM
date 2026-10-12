using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RetryEventProjectorExecution(
    long eventId,
    string projectorName,
    DateTime nowUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        TimestampTz(nowUtc),
        Text(updatedTimestamp));
}
