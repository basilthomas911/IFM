using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEventProjectorOperationalStatePage(
    string projectorName,
    string status,
    long afterEventId,
    int batchSize) : IBindValue
{
    public object Bind() => Values(
        Text(projectorName),
        Text(status),
        Bigint(afterEventId),
        Integer(batchSize));
}
