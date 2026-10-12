using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TryTerminalizeEventProjectorExecutionWithOutbox(
    TryTerminalizeEventProjectorExecution Transition,
    string effectKind,
    string messageId,
    string eventTypeName,
    byte[] eventPayload,
    DateTime createdAtUtc) : IBindValue
{
    public object Bind()
    {
        var values = (Npgsql.NpgsqlParameter[])Transition.Bind();
        return Values(
            [.. values,
             Text(effectKind),
             Text(messageId),
             Text(eventTypeName),
             Bytea(eventPayload),
             TimestampTz(createdAtUtc)]);
    }
}
