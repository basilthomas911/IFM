using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionPricerDevice(
    int DeviceId,
    string DeviceName,
    int SpreadPaths,
    int VolatilityPaths,
    int MaxBatchSize,
    string OptionType,
    bool Enabled) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql
        ? Values(Integer(DeviceId), Text(DeviceName), Integer(SpreadPaths), Integer(VolatilityPaths), Integer(MaxBatchSize), Text(OptionType), Boolean(Enabled))
        : new object?[]
    {
        DeviceId,
        DeviceName,
        SpreadPaths,
        VolatilityPaths,
        MaxBatchSize,
        OptionType,
        Enabled
    };
}
