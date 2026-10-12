using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
