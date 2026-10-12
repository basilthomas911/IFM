using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTradeSignalIndex(string scope, string entryId, long sequenceId, string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(scope),
        Text(entryId),
        Bigint(sequenceId),
        Text(contractId),
        Date(valueDate),
        Text(timePeriod)
    } : new object?[] { scope, entryId, sequenceId, contractId, valueDate, timePeriod };
}
