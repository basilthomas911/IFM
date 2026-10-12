using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesSymbolProjectionStateV3(string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbol };
}
