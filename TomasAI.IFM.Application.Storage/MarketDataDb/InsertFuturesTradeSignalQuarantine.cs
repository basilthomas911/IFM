using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTradeSignalQuarantine(string fingerprint, string sourcePayload, string reason, DateTime quarantinedOn) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(fingerprint),
        Text(sourcePayload),
        Text(reason),
        TimestampTz(quarantinedOn)
    } : new object?[] { fingerprint, sourcePayload, reason, quarantinedOn };
}
