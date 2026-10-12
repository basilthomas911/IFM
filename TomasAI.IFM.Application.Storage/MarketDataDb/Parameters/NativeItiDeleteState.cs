using Npgsql;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
namespace TomasAI.IFM.Application.Storage.MarketDataDb.Parameters;
/// <summary>Identifies the exact dated timeframe state being removed.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeItiDeleteState(string contractId, string timePeriod, DateOnly bucketStart, DateOnly valueDate) : IBindValue
{
    public object Bind() => new NpgsqlParameter[] { Text(contractId), Text(timePeriod), Date(bucketStart), Date(valueDate) };
}
