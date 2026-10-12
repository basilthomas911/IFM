using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Binds an exact workflow/input revision preparation lookup.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeCompositionReadParameters(Guid WorkflowId, long InputRevision, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Uuid(WorkflowId), Bigint(InputRevision)) : new object[] { WorkflowId, InputRevision };
}
