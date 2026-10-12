using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MarkEventProjectorOutboxPublished(
    string projectorName,
    long eventId,
    string effectKind,
    Guid dispatchToken,
    DateTime nowUtc,
    DateTime publishedAtUtc) : IBindValue
{
    public object Bind() => Values(
        Text(projectorName),
        Bigint(eventId),
        Text(effectKind),
        Uuid(dispatchToken),
        TimestampTz(nowUtc),
        TimestampTz(publishedAtUtc));
}
