using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEventLogFromSnapshotLastNRange(
    long eventStreamId,
    int snapshotEventNameId,
    int rangeEventNameId,
    int lastNRange) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventStreamId),
        Integer(snapshotEventNameId),
        Integer(rangeEventNameId),
        Integer(lastNRange));
}
