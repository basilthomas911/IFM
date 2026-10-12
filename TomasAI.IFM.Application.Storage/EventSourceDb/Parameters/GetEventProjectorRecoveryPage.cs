using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEventProjectorRecoveryPage(
    string projectorName,
    string eventNames,
    long afterEventId,
    DateTime nowUtc,
    int batchSize) : IBindValue
{
    public object Bind() => Values(
        Text(projectorName),
        Text(eventNames),
        Bigint(afterEventId),
        TimestampTz(nowUtc),
        Integer(batchSize));
}
