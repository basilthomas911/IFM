using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateCommandLog(Guid commandId, string commandStatus, DateTime updateTimestamp) : IBindValue
{
    public object Bind() => Values(Uuid(commandId), Text(commandStatus), Timestamp(updateTimestamp));
}
