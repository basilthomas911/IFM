using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PublishConfiguration(
    short PublishedStatus, DateTime EffectiveFromUtc, Guid ParameterSetId, int Version, short DraftStatus)
    : IBindValue
{
    public object Bind() => Values(
        Smallint(PublishedStatus), TimestampTz(EffectiveFromUtc), Uuid(ParameterSetId), Integer(Version),
        Smallint(DraftStatus));
}
