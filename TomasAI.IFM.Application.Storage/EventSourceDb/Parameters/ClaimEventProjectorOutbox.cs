using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ClaimEventProjectorOutbox(
    string projectorName,
    Guid dispatchToken,
    DateTime dispatchLeaseExpiresAtUtc,
    DateTime nowUtc,
    int batchSize) : IBindValue
{
    public object Bind() => Values(
        Text(projectorName),
        Uuid(dispatchToken),
        TimestampTz(dispatchLeaseExpiresAtUtc),
        TimestampTz(nowUtc),
        Integer(batchSize));
}
