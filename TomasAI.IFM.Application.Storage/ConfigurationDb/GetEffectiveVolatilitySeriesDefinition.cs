using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEffectiveVolatilitySeriesDefinition(
    string Environment,
    string SeriesId,
    DateTime EffectiveAtUtc) : IBindValue
{
    public object Bind() => Values(Text(Environment), Text(SeriesId), TimestampTz(EffectiveAtUtc));
}
