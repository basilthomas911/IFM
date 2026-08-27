using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

internal readonly record struct InsertConfigurationDraft(
    Guid ParameterSetId, int Version, short SchemaVersion, short Status,
    string PayloadJson, string PayloadSha256, string Description, DateTime CreatedUtc, string CreatedBy)
    : IBindValue
{
    public object Bind() => Values(
        Uuid(ParameterSetId), Integer(Version), Smallint(SchemaVersion), Smallint(Status),
        Text(PayloadJson), Text(PayloadSha256), Text(Description), TimestampTz(CreatedUtc), Text(CreatedBy));
}

internal readonly record struct PublishConfiguration(
    short PublishedStatus, DateTime EffectiveFromUtc, Guid ParameterSetId, int Version, short DraftStatus)
    : IBindValue
{
    public object Bind() => Values(
        Smallint(PublishedStatus), TimestampTz(EffectiveFromUtc), Uuid(ParameterSetId), Integer(Version),
        Smallint(DraftStatus));
}

internal readonly record struct RetireConfiguration(
    short RetiredStatus, DateTime RetiredAtUtc, Guid ParameterSetId, int Version, short PublishedStatus)
    : IBindValue
{
    public object Bind() => Values(
        Smallint(RetiredStatus), TimestampTz(RetiredAtUtc), Uuid(ParameterSetId), Integer(Version),
        Smallint(PublishedStatus));
}

internal readonly record struct GetConfiguration(Guid ParameterSetId, int Version) : IBindValue
{
    public object Bind() => Values(Uuid(ParameterSetId), Integer(Version));
}

internal readonly record struct ResolveConfiguration(
    short PublishedStatus,
    DateTime EffectiveAtUtc,
    short TargetHorizon) : IBindValue
{
    public object Bind() => Values(
        Smallint(PublishedStatus), TimestampTz(EffectiveAtUtc), Smallint(TargetHorizon),
        TimestampTz(EffectiveAtUtc));
}
