using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesTdiSignal(
    string contractId,
    string timePeriod,
    string configurationId,
    DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Text(timePeriod), Text(configurationId), Date(valueDate)) : new object?[] { contractId, timePeriod, configurationId, valueDate };
}
