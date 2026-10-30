using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesSymbolProjectionStateV3(string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbol };
}
