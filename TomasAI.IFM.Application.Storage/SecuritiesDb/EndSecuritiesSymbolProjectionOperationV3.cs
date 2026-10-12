using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EndSecuritiesSymbolProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, symbol };
}
