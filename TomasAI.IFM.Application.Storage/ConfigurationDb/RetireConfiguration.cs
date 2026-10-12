using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RetireConfiguration(
    short RetiredStatus, DateTime RetiredAtUtc, Guid ParameterSetId, int Version, short PublishedStatus)
    : IBindValue
{
    public object Bind() => Values(
        Smallint(RetiredStatus), TimestampTz(RetiredAtUtc), Uuid(ParameterSetId), Integer(Version),
        Smallint(PublishedStatus));
}
