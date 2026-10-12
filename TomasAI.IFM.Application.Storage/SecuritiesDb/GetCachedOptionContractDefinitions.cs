using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetCachedOptionContractDefinitions(
    string symbol, Guid generation, DateOnly expiryDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(symbol),Uuid(generation),Date(expiryDate)) : new object?[] { symbol, generation, expiryDate };
}
