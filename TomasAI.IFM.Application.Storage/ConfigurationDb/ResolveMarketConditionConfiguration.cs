using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
