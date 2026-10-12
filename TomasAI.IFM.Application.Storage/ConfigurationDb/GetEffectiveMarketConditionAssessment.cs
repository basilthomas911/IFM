using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEffectiveMarketConditionAssessment(
    string MarketProfileId,
    string InstrumentRoot,
    short TargetHorizon,
    DateTime EffectiveAtUtc) : IBindValue
{
    public object Bind() => Values(
        Text(MarketProfileId), Text(InstrumentRoot), Smallint(TargetHorizon), TimestampTz(EffectiveAtUtc));
}
