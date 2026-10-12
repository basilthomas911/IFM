using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Binds an immutable provider-evidence lookup without object-array type inference.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeOptionEvidenceReadParameters(string ContractId, DateOnly ValueDate, string SourceId, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Text(ContractId), Date(ValueDate), Text(SourceId)) : new object[] { ContractId, ValueDate, SourceId };
}
