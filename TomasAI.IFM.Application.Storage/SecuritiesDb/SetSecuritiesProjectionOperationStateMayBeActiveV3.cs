using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct SetSecuritiesProjectionOperationStateMayBeActiveV3(bool stateMayBeActive, string projectionName, Guid operationId, bool expectedStateMayBeActive) : IBindValue
{
    public object Bind() => new object?[] { stateMayBeActive, projectionName, operationId, expectedStateMayBeActive };
}
