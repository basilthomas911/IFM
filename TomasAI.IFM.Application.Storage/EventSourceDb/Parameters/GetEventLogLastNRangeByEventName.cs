using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEventLogLastNRangeByEventName(
    long eventStreamId,
    int eventNameId,
    int lastNRange) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventStreamId),
        Integer(eventNameId),
        Integer(lastNRange));
}
