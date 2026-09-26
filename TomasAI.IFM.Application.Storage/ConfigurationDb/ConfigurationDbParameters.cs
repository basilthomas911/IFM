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

internal readonly record struct ResolveMarketConditionConfiguration(
    short PublishedStatus,
    DateTime EffectiveAtUtc,
    int FundId,
    string InstrumentRoot,
    short TargetHorizon) : IBindValue
{
    public object Bind() => Values(
        Smallint(PublishedStatus), TimestampTz(EffectiveAtUtc), Integer(FundId), Text(InstrumentRoot),
        Smallint(TargetHorizon), TimestampTz(EffectiveAtUtc));
}

internal readonly record struct GetLookupDefinitions(string GroupName) : IBindValue
{
    public object Bind() => Values(Text(GroupName));
}

internal readonly record struct InsertMarketConditionAssessmentDraft(
    Guid Id,
    int Version,
    short SchemaVersion,
    string MarketProfileId,
    string InstrumentRoot,
    short TargetHorizon,
    string PayloadJson,
    string PayloadSha256,
    string Description,
    DateTime CreatedUtc,
    string CreatedBy) : IBindValue
{
    public object Bind() => Values(
        Uuid(Id), Integer(Version), Smallint(SchemaVersion), Text(MarketProfileId), Text(InstrumentRoot),
        Smallint(TargetHorizon), Text(PayloadJson), Text(PayloadSha256), Text(Description),
        TimestampTz(CreatedUtc), Text(CreatedBy));
}

internal readonly record struct GetEffectiveMarketConditionAssessment(
    string MarketProfileId,
    string InstrumentRoot,
    short TargetHorizon,
    DateTime EffectiveAtUtc) : IBindValue
{
    public object Bind() => Values(
        Text(MarketProfileId), Text(InstrumentRoot), Smallint(TargetHorizon), TimestampTz(EffectiveAtUtc));
}

internal readonly record struct InsertVolatilitySeriesDefinition(
    string SeriesId,
    string MethodologyVersion,
    string Environment,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveUntilUtc,
    string PayloadJson,
    string PayloadSha256,
    string ApprovedConfigurationVersion,
    string Owner,
    string ApprovalEvidenceId) : IBindValue
{
    public object Bind() => Values(
        Text(SeriesId), Text(MethodologyVersion), Text(Environment), TimestampTz(EffectiveFromUtc),
        TimestampTz(EffectiveUntilUtc), Text(PayloadJson), Text(PayloadSha256),
        Text(ApprovedConfigurationVersion), Text(Owner), Text(ApprovalEvidenceId));
}

internal readonly record struct GetVolatilitySeriesDefinition(
    string SeriesId,
    string MethodologyVersion) : IBindValue
{
    public object Bind() => Values(Text(SeriesId), Text(MethodologyVersion));
}

internal readonly record struct GetEffectiveVolatilitySeriesDefinition(
    string Environment,
    string SeriesId,
    DateTime EffectiveAtUtc) : IBindValue
{
    public object Bind() => Values(Text(Environment), Text(SeriesId), TimestampTz(EffectiveAtUtc));
}

internal readonly record struct GetLegacyParameterVersions(Guid? SetId, int Version, int Offset) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Integer(Version), Integer(Offset));
}

internal readonly record struct GetParameterSchema(string ComponentCode, int Version) : IBindValue
{
    public object Bind() => Values(Text(ComponentCode), Integer(Version));
}

internal readonly record struct GetParameterSets(
    string ComponentCode,
    Guid? SetId,
    string AfterName,
    Guid? AfterSetId,
    int AfterVersion,
    int Limit) : IBindValue
{
    public object Bind() => Values(
        Text(ComponentCode), Uuid(SetId), Text(AfterName), Uuid(AfterSetId), Integer(AfterVersion), Integer(Limit));
}

internal readonly record struct CatalogIdentity(short Kind, Guid Id) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id));
}

internal readonly record struct InsertCatalogIdentity(short Kind, Guid Id, string Code, DateTime CreatedUtc, string CreatedBy) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id), Text(Code), TimestampTz(CreatedUtc), Text(CreatedBy));
}

internal readonly record struct InsertCatalogVersion(string Json, string Hash, DateTime CreatedUtc, string CreatedBy) : IBindValue
{
    public object Bind() => Values(Text(Json), Text(Hash), TimestampTz(CreatedUtc), Text(CreatedBy));
}

internal readonly record struct CatalogVersion(short Kind, Guid Id, int Version) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id), Integer(Version));
}

internal readonly record struct ListCatalogs(short Kind, string AfterCode, int Limit) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Text(AfterCode), Integer(Limit));
}

internal readonly record struct PublishCatalog(short Kind, Guid Id, int Version, DateTime EffectiveFromUtc, string PublishedBy, string Hash) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id), Integer(Version), TimestampTz(EffectiveFromUtc), Text(PublishedBy), Text(Hash));
}

internal readonly record struct RetireCatalog(short Kind, Guid Id, int Version, DateTime RetiredAtUtc, string RetiredBy) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id), Integer(Version), TimestampTz(RetiredAtUtc), Text(RetiredBy));
}

internal readonly record struct TextValue(string Value) : IBindValue
{
    public object Bind() => Values(Text(Value));
}

internal readonly record struct GuidValue(Guid Value) : IBindValue
{
    public object Bind() => Values(Uuid(Value));
}

internal readonly record struct LongValue(long Value) : IBindValue
{
    public object Bind() => Values(Bigint(Value));
}

internal readonly record struct GuidVersion(Guid Id, int Version) : IBindValue
{
    public object Bind() => Values(Uuid(Id), Integer(Version));
}

internal readonly record struct UpsertAssignment(Guid AssignmentId, long Revision, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(AssignmentId), Bigint(Revision), Text(Body));
}

internal readonly record struct InsertAssignmentRevision(Guid AssignmentId, long Revision, Guid OperationId, string RequestHash, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(AssignmentId), Bigint(Revision), Uuid(OperationId), Text(RequestHash), Text(Body));
}

internal readonly record struct InsertParameterAudit(Guid OperationId, Guid EntityId, long Revision, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId), Uuid(EntityId), Bigint(Revision), Text(Body));
}

internal readonly record struct UpsertParameterSet(Guid SetId, string ComponentCode, string Name, string Description, long Revision) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Text(ComponentCode), Text(Name), Text(Description), Bigint(Revision));
}

internal readonly record struct UpsertParameterSetVersion(Guid SetId, int Version, string Hash, short Status, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Integer(Version), Text(Hash), Smallint(Status), Text(Body));
}

internal readonly record struct InsertParameterOperation(Guid OperationId, Guid SetId, long Revision, string RequestHash, int Version) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId), Uuid(SetId), Bigint(Revision), Text(RequestHash), Integer(Version));
}

internal readonly record struct LegacyParameterReference(string Kind, Guid LegacySetId, int LegacyVersion, Guid GenericSetId, int GenericVersion, string Hash, string Codec) : IBindValue
{
    public object Bind() => Values(Text(Kind), Uuid(LegacySetId), Integer(LegacyVersion), Uuid(GenericSetId), Integer(GenericVersion), Text(Hash), Text(Codec));
}

internal readonly record struct VerifyLegacyParameterReference(string Kind, Guid LegacySetId, int LegacyVersion, Guid GenericSetId, string Hash, string Codec) : IBindValue
{
    public object Bind() => Values(Text(Kind), Uuid(LegacySetId), Integer(LegacyVersion), Uuid(GenericSetId), Text(Hash), Text(Codec));
}

internal readonly record struct StartupReport(Guid RunId, long Revision, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(RunId), Bigint(Revision), Text(Body));
}

internal readonly record struct StartupRun(Guid RunId, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(RunId), Text(Body));
}
