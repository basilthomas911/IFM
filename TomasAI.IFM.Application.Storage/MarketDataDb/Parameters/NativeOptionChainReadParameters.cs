using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Binds the environment for current option-chain policy reads in PostgreSQL.</summary>
internal readonly record struct NativeOptionChainPublicationReadParameters(string Environment) : IBindValue
{
    public object Bind() => Values(Text(Environment));
}

/// <summary>Binds an immutable option-chain policy identity in PostgreSQL.</summary>
internal readonly record struct NativeOptionChainVersionReadParameters(Guid SetId, int Version) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Integer(Version));
}
