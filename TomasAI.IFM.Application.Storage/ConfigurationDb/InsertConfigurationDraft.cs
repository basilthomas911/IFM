using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertConfigurationDraft(
    Guid ParameterSetId, int Version, short SchemaVersion, short Status,
    string PayloadJson, string PayloadSha256, string Description, DateTime CreatedUtc, string CreatedBy)
    : IBindValue
{
    public object Bind() => Values(
        Uuid(ParameterSetId), Integer(Version), Smallint(SchemaVersion), Smallint(Status),
        Text(PayloadJson), Text(PayloadSha256), Text(Description), TimestampTz(CreatedUtc), Text(CreatedBy));
}
