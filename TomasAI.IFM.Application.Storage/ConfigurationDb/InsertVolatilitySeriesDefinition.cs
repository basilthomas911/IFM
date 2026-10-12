using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
