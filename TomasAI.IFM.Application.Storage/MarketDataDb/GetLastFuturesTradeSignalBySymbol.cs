using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesTradeSignalBySymbol(List<string> contractIds, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql requires its Array type marker combined with the element type.")]
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        new NpgsqlParameter<string[]> { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, TypedValue = [.. contractIds] },
        Date(valueDate)
    } : new object?[] { contractIds, valueDate };
}
