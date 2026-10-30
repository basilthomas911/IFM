using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteSecuritiesProjectionOperationV3(HashSet<Guid> activeOperations, string projectionName, Guid generation, HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, generation, expectedActiveOperations };
}
