using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ReleaseEventProjectorOutbox(
    string projectorName,
    long eventId,
    string effectKind,
    Guid dispatchToken,
    DateTime nowUtc,
    string status,
    DateTime? nextAttemptAtUtc,
    string lastError) : IBindValue
{
    public object Bind() => Values(
        Text(projectorName),
        Bigint(eventId),
        Text(effectKind),
        Uuid(dispatchToken),
        TimestampTz(nowUtc),
        Text(status),
        TimestampTz(nextAttemptAtUtc),
        Text(lastError));
}
