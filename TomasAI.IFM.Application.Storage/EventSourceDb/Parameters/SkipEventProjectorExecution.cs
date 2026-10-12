using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct SkipEventProjectorExecution(
    long eventId,
    string projectorName,
    string reason,
    DateTime nowUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Text(reason),
        TimestampTz(nowUtc),
        Text(updatedTimestamp));
}
