using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteSecuritiesProjectionOperationV3(HashSet<Guid> activeOperations, string projectionName, Guid generation, HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, generation, expectedActiveOperations };
}
