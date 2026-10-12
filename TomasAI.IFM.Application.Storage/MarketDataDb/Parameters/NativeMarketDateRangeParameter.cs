using Npgsql;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
namespace TomasAI.IFM.Application.Storage.MarketDataDb.Parameters;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeMarketDateRangeParameter(DateOnly StartDate, DateOnly EndDate) : IBindValue
{
    public object Bind() => new NpgsqlParameter[] { Date(StartDate),Date(EndDate) };
}
