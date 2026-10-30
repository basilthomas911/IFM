using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOptionContractExpiries(
    string symbol, Guid generation, DateOnly fromExpiry, DateOnly throughExpiry) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(symbol),Uuid(generation),Date(fromExpiry),Date(throughExpiry)) : new object?[] { symbol, generation, fromExpiry, throughExpiry };
}
