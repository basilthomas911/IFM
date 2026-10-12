using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertEventLogExpectedVersion(
    long eventStreamId,
    int eventNameId,
    byte[] eventData,
    Guid commandId,
    string eventTimestamp,
    long expectedStreamVersion) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId), Integer(eventNameId), Bytea(eventData), Uuid(commandId),
        Text(eventTimestamp), Bigint(expectedStreamVersion));
}
