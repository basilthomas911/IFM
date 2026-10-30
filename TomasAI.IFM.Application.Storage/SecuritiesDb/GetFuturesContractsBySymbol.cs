using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesContractsBySymbol(string symbol) : IBindValue
{
    public bool PostgreSql { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with their element type.")]
    public object Bind() => PostgreSql ? new object?[] { Text(symbol) } : new object?[] { symbol };
}
