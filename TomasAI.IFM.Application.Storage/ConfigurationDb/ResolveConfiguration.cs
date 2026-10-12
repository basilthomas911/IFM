using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ResolveConfiguration(
    short PublishedStatus,
    DateTime EffectiveAtUtc,
    short TargetHorizon) : IBindValue
{
    public object Bind() => Values(
        Smallint(PublishedStatus), TimestampTz(EffectiveAtUtc), Smallint(TargetHorizon),
        TimestampTz(EffectiveAtUtc));
}
