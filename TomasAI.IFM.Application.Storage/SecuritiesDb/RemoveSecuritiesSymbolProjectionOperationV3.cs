using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RemoveSecuritiesSymbolProjectionOperationV3(Guid operationId, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName, symbol };
}
