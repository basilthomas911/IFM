using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSecuritiesProjectionOperationV3(string projectionName, Guid operationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId, startedOn };
}
