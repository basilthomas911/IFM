using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertActorCommandLog(Guid commandId, string streamId, string aggregateName, string commandName, string commandTimestamp, string commandStatus, string commandData) : IBindValue
{
    public object Bind() => Values(Uuid(commandId), Text(streamId), Text(aggregateName), Text(commandName), Text(commandTimestamp), Text(commandStatus), Text(commandData));
}
