using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertMarketOutlookSnapshot(
    string contractId,
    DateOnly valueDate,
    long revision,
    DateTime updatedOn,
    byte[] eodData,
    byte[]? futuresTradeSignal,
    string missingInputs,
    byte[] snapshot) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Bigint(revision),
        TimestampTz(updatedOn),
        Bytea(eodData),
        Bytea(futuresTradeSignal),
        Text(missingInputs),
        Bytea(snapshot)
    } : new object?[]
    {
        contractId, valueDate, revision, updatedOn, eodData, futuresTradeSignal, missingInputs, snapshot
    };
}
