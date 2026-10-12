using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Binds the complete immutable evidence identity, digest and accepted payload.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeOptionEvidenceWriteParameters(string ContractId, DateOnly ValueDate, string SourceId, string Digest, byte[] Payload, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Text(ContractId), Date(ValueDate), Text(SourceId), Text(Digest), Bytea(Payload)) : new object[] { ContractId, ValueDate, SourceId, Digest, Payload };
}
