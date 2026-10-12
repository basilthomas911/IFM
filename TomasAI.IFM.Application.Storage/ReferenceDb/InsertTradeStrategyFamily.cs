using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeStrategyFamily(string catalog, int tradeStrategyFamilyId, long definitionVersion, string systemKey, string family, string strategy, string timeFrame, string symbol, string currency, string description, string state, DateTime createdOnUtc, string createdBy) : IBindValue
{ public bool PostgreSql { get; init; } public object Bind() => PostgreSql ? Values(Text(catalog), Integer(tradeStrategyFamilyId), Bigint(definitionVersion), Text(systemKey), Text(family), Text(strategy), Text(timeFrame), Text(symbol), Text(currency), Text(description), Text(state), TimestampTz(createdOnUtc), Text(createdBy)) : new object?[] { catalog, tradeStrategyFamilyId, definitionVersion, systemKey, family, strategy, timeFrame, symbol, currency, description, state, createdOnUtc, createdBy }; }
