using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Binds an immutable accepted composition preparation.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeCompositionWriteParameters(Guid WorkflowId, long InputRevision, byte[] Payload, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Uuid(WorkflowId), Bigint(InputRevision), Bytea(Payload)) : new object[] { WorkflowId, InputRevision, Payload };
}
