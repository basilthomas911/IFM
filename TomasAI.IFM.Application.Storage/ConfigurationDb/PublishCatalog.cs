using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PublishCatalog(short Kind, Guid Id, int Version, DateTime EffectiveFromUtc, string PublishedBy, string Hash) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id), Integer(Version), TimestampTz(EffectiveFromUtc), Text(PublishedBy), Text(Hash));
}
