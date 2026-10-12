using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DownloadLogReadParameters(
    string Dataset,
    string Provider,
    string Scope,
    DateOnly ValueDate,
    DateTime RequestedAtUtc,
    Guid ImportCommandId,
    int RowLimit,
    bool Exact = false) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind()
    {
        if (PostgreSql)
        {
            if (Exact) return new NpgsqlParameter[] { Text(Dataset),Text(Provider),Text(Scope),Date(ValueDate),TimestampTz(RequestedAtUtc),Uuid(ImportCommandId) };
            if (ImportCommandId == Guid.Empty) return new NpgsqlParameter[] { Text(Dataset),Text(Provider),Text(Scope),Date(ValueDate),Integer(RowLimit) };
            return new NpgsqlParameter[] { Text(Dataset),Text(Provider),Text(Scope),Date(ValueDate),TimestampTz(RequestedAtUtc),Uuid(ImportCommandId),Integer(RowLimit) };
        }
        if (Exact) return new object[] { Dataset, Provider, Scope, ValueDate, RequestedAtUtc, ImportCommandId };
        if (ImportCommandId == Guid.Empty) return new object[] { Dataset, Provider, Scope, ValueDate, RowLimit };
        return new object[] { Dataset, Provider, Scope, ValueDate, RequestedAtUtc, ImportCommandId, RowLimit };
    }
}
