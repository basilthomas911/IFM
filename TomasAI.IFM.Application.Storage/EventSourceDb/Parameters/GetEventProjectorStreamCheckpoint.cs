using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEventProjectorStreamCheckpoint(string projectorName, long eventStreamId) : IBindValue
{
    public object Bind() => Values(Text(projectorName), Bigint(eventStreamId));
}
