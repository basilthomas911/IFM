using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BeginMarketDataProjectionOperation(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BeginMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, scopeKey };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ClaimMarketDataImportOwnership(
    string dataset, string logicalKey, Guid commandId, bool mayWrite, DateTime createdOn) : IBindValue
{
    public object Bind() => new object?[] { dataset, logicalKey, commandId, mayWrite, createdOn };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteMarketDataProjectionGuardOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        generation,
        activeOperations,
        completedOn,
        projectionName,
        scopeKey,
        expectedActiveOperations
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        completedOn,
        projectionName,
        scopeKey,
        generation,
        expectedActiveOperations
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteMarketDataProjectionState(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations,
    long sourceRowCount,
    long projectedRowCount,
    string sourceFingerprint,
    string projectedFingerprint,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        sourceRowCount,
        projectedRowCount,
        sourceFingerprint,
        projectedFingerprint,
        completedOn,
        projectionName,
        generation,
        expectedActiveOperations
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompositionPreparationParameters(object[] Values) : IBindValue
{
    public object Bind() => Values;
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteEconomicCalendarV2(string countryCode, int monthBucket, DateTime eventDate, string eventName) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(countryCode),
        Integer(monthBucket),
        TimestampTz(eventDate),
        Text(eventName)
    } : new object?[] { countryCode, monthBucket, eventDate, eventName };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesAdxSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength),
        Date(valueDate)
    } : new object?[] { contractId, timePeriod, periodLength, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesAtrSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength),
        Date(valueDate)
    } : new object?[] { contractId, timePeriod, periodLength, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesBarData(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate)
    } : new object?[] { contractId, symbol, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate)) : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesEodDataByMonth(int yearMonth, DateOnly valueDate, string contractId) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Integer(yearMonth), Date(valueDate), Text(contractId)) : new object?[] { yearMonth, valueDate, contractId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesItiSignal(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod)
    } : new object?[] { contractId, valueDate, timePeriod };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesItiSignalByContractDay(string contractId, DateOnly valueDate, string intrinsicTimeMode, long sequenceId, string timePeriod, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode, sequenceId, timePeriod, intrinsicTimeTrend, intrinsicTimeGroupId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesItiSignalByContractMonth(string contractId, int yearMonth, DateOnly valueDate, long sequenceId, string timePeriod, string intrinsicTimeMode, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesItiSignalByTrendModeMonth(string contractId, string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth, DateOnly valueDate, long sequenceId, string timePeriod, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeGroupId };
}




[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionTickPriceData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesTickDataByTime(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteMarketDataProjectionMutation(string projectionName, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKey, mutationId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteMarketHoliday(string currencyType, DateOnly holidayDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(currencyType),
        Date(holidayDate)
    } : new object?[] { currencyType, holidayDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteMarketHolidays(string currencyType) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(currencyType)
    } : new object?[] { currencyType };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteRateOfReturn(string symbol, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(symbol),
        Date(valueDate)
    } : new object?[] { symbol, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(orderId),
        Integer(tradeId)
    } : new object?[] { orderId, tradeId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteVixFuturesContractIndex(int bucket, string contractId) : IBindValue
{
    public object Bind() => new object?[] { bucket, contractId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteYieldCurveRate(DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(valueDate)
    } : new object?[] { valueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DownloadLogParameters(
    string Dataset,
    string Provider,
    string Scope,
    DateOnly ValueDate,
    DateTime RequestedAtUtc,
    Guid ImportCommandId,
    Guid LogCommandId,
    Guid SourceTerminalEventId,
    short SchemaVersion,
    string Status,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long ElapsedMilliseconds,
    long? DownloadedRecordCount,
    long? PersistedRecordCount,
    string? ErrorCode,
    string? ErrorMessage,
    string PayloadSha256,
    DateTime ProjectedAtUtc) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(Dataset),
        Text(Provider),
        Text(Scope),
        Date(ValueDate),
        TimestampTz(RequestedAtUtc),
        Uuid(ImportCommandId),
        Uuid(LogCommandId),
        Uuid(SourceTerminalEventId),
        Smallint(SchemaVersion),
        Text(Status),
        TimestampTz(StartedAtUtc),
        TimestampTz(FinishedAtUtc),
        Bigint(ElapsedMilliseconds),
        Bigint(DownloadedRecordCount),
        Bigint(PersistedRecordCount),
        Text(ErrorCode),
        Text(ErrorMessage),
        Text(PayloadSha256),
        TimestampTz(ProjectedAtUtc)
    } : new object?[]
    {
        Dataset,
        Provider,
        Scope,
        ValueDate,
        RequestedAtUtc,
        ImportCommandId,
        LogCommandId,
        SourceTerminalEventId,
        SchemaVersion,
        Status,
        StartedAtUtc,
        FinishedAtUtc,
        ElapsedMilliseconds,
        DownloadedRecordCount,
        PersistedRecordCount,
        ErrorCode,
        ErrorMessage,
        PayloadSha256,
        ProjectedAtUtc
    };
}



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


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EndMarketDataProjectionOperation(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EndMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, scopeKey };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FailMarketDataProjectionMutation(
    string projectionName,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { startedOn, projectionName, mutationId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FailMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { startedOn, projectionName, scopeKey, mutationId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetCurrentFuturesEodDataByDateRange(int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetCurrentFuturesEodDataByMonth(int yearMonth, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEconomicCalendarCountryCodes(int lookupId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(lookupId)
    } : new object?[] { lookupId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEconomicCalendars(string countryCode, int monthBucket, DateTime startDate, DateTime endDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(countryCode),
        Integer(monthBucket),
        TimestampTz(startDate),
        TimestampTz(endDate)
    } : new object?[] { countryCode, monthBucket, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEconomicCalendarV2ById(string countryCode, int monthBucket, DateTime eventDate, string eventName) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(countryCode),
        Integer(monthBucket),
        TimestampTz(eventDate),
        Text(eventName)
    } : new object?[] { countryCode, monthBucket, eventDate, eventName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetEconomicCalendarV2CommandId(string countryCode, int monthBucket, DateTime eventDate, string eventName) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(countryCode),
        Integer(monthBucket),
        TimestampTz(eventDate),
        Text(eventName)
    } : new object?[] { countryCode, monthBucket, eventDate, eventName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate),
        TimestampTz(startDate),
        TimestampTz(endDate)
    } : new object?[] { contractId, symbol, valueDate, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesBarDataCount(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate)
    } : new object?[] { contractId, symbol, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesDataId(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate)) : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesEodClosingPrices(string contractId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(startDate),
        Date(endDate)
    } : new object?[] { contractId, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate)) : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesEodDataByDateRange(string contractId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(startDate), Date(endDate)) : new object?[] { contractId, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesIntraDayData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate)) : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalContractIdsByDate(DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(valueDate)
    } : new object?[] { valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignals(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod)
    } : new object?[] { contractId, valueDate, timePeriod };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalsByContractDayMode(string contractId, DateOnly valueDate, string intrinsicTimeMode) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalsByContractDayModeAfterSequence(string contractId, DateOnly valueDate, string intrinsicTimeMode, long sequenceId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode, sequenceId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalsByContractMonth(string contractId, int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalsByTrendModeMonth(string contractId, string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalsCanonicalByContract(string contractId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId)
    } : new object?[] { contractId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiSignalsCanonicalByContractDay(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesItiTimeFrameState(string contractId, string timePeriod, DateOnly calendarBucketStart) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Date(calendarBucketStart)
    } : new object?[] { contractId, timePeriod, calendarBucketStart };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesOptionChainQuoteData(
    string underlyingContractId, DateOnly expiryDate, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(underlyingContractId),
        Date(expiryDate),
        Date(valueDate)
    } : new object?[] { underlyingContractId, expiryDate, valueDate };
}








[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesOptionTickPriceData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Bigint(tickId)
    } : new object?[] { contractId, valueDate, tickId };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesRsiSignalsForTrend(string contractId, string timePeriod, int periodLength, DateOnly valueDate, TimeOnly startTime, TimeOnly endTime) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength),
        Date(valueDate),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = startTime },
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = endTime }
    } : new object?[] { contractId, timePeriod, periodLength, valueDate, startTime, endTime };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesTickData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesTickDataPriceByTickId(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}


[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesTickHLVData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesTradeSignalIdByValueDate(string scope) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(scope)
    } : new object?[] { scope };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesVwapSignalHistory(
    string contractId, DateOnly valueDate, string configurationId) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(configurationId)
    } : new object[] { contractId, valueDate, configurationId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesAdxDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength)
    } : new object?[] { contractId, timePeriod, periodLength };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesAdxSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength),
        Date(valueDate)
    } : new object?[] { contractId, timePeriod, periodLength, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesAtrDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength)
    } : new object?[] { contractId, timePeriod, periodLength };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesAtrSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength),
        Date(valueDate)
    } : new object?[] { contractId, timePeriod, periodLength, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesBarData(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate)
    } : new object?[] { contractId, symbol, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate)) : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesItiSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesItiSignalByTimePeriod(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod)
    } : new object?[] { contractId, valueDate, timePeriod };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesItiSignalByTrendModeMonth(string contractId, string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesItiSignalTrendDirectionChange(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesItiSignalTrendExtremeChange(string contractId, DateOnly valueDate, long lastTrendDirectionChangedSequenceId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, lastTrendDirectionChangedSequenceId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesItiSignalTrendReversalChange(string contractId, DateOnly valueDate, long lastTrendDirectionChangedSequenceId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, lastTrendDirectionChangedSequenceId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesMacdDailySignal(
    string contractId,
    string timePeriod,
    int signalEmaPeriod,
    int fastEmaPeriod,
    int slowEmaPeriod) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(signalEmaPeriod),
        Integer(fastEmaPeriod),
        Integer(slowEmaPeriod)
    } : new object?[] { contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesMacdSignal(
    string contractId,
    string timePeriod,
    int signalEmaPeriod,
    int fastEmaPeriod,
    int slowEmaPeriod,
    DateOnly valueDate) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(signalEmaPeriod),
        Integer(fastEmaPeriod),
        Integer(slowEmaPeriod),
        Date(valueDate)
    } : new object?[] { contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesOptionTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesOptionTickDataId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesOptionTickPriceData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesRsiDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength)
    } : new object?[] { contractId, timePeriod, periodLength };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesRsiSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength),
        Date(valueDate)
    } : new object?[] { contractId, timePeriod, periodLength, valueDate };
}


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


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesTickDataByTickTime(string contractId, DateOnly valueDate, TimeOnly tickTime) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickTime };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesTradeSignal(string scope) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(scope)
    } : new object?[] { scope };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesTradeSignalById(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod)
    } : new object?[] { contractId, valueDate, timePeriod };
}


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


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastRateOfReturn(string symbol) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(symbol)
    } : new object?[] { symbol };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLatestFuturesRegimeSignal(
    string seriesKey,
    string timePeriod,
    string configurationId,
    int yearMonth,
    DateTime marketDataAsOf) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(seriesKey),
        Text(timePeriod),
        Text(configurationId),
        Integer(yearMonth),
        TimestampTz(marketDataAsOf)
    } : new object[]
    {
        seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLatestFuturesVwapSignal(
    string contractId, DateOnly valueDate, string configurationId) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(configurationId)
    } : new object[] { contractId, valueDate, configurationId };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLatestFuturesVxTermStructureSignal(
    DateOnly valueDate, string configurationId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(valueDate),
        Text(configurationId)
    } : new object[] { valueDate, configurationId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketDataImportOwnership(string dataset, string logicalKey) : IBindValue
{
    public object Bind() => new object?[] { dataset, logicalKey };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketDataProjectionMonths(string projectionName, int yearMonth) : IBindValue
{
    public object Bind() => new object?[] { projectionName, yearMonth };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketDataProjectionMutation(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketDataProjectionScopeStatesV3(
    string projectionName,
    ICollection<string> scopeKeys) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKeys };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketDataProjectionState(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketHolidays(string currencyType) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(currencyType)
    } : new object?[] { currencyType };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketHolidaysByDateRange(string currencyType, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(currencyType),
        Date(startDate),
        Date(endDate)
    } : new object?[] { currencyType, startDate, endDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketOutlookSnapshot(
    string contractId,
    DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketOutlookWorkingState(
    string contractId,
    DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMinFuturesTickDataTickId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(orderId),
        Integer(tradeId)
    } : new object?[] { orderId, tradeId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetVixFuturesContractIds(int bucket) : IBindValue
{
    public object Bind() => new object?[] { bucket };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetVixFuturesEodDataThroughDate(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetYesterdaysFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate)
    } : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetYesterdaysFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate)) : new object?[] { contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetYieldCurveRate(DateOnly valueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(valueDate)
    } : new object?[] { valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetYieldCurveRates(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(startDate),
        Date(endDate)
    } : new object?[] { startDate, endDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetYieldCurveRateYears(int lookupId) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(lookupId)
    } : new object?[] { lookupId };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalObservationParameter(FuturesTradeSessionBarReadModel Value) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(Value.MarketSeriesIdentity.Format()),
        Text(Value.TimeFrame.ToString()),
        Integer(checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month)),
        TimestampTz(Value.IntervalEndUtc.UtcDateTime),
        Uuid(Value.ObservationId.Value),
        Text(Value.ContractId),
        Date(Value.ValueDate),
        TimestampTz(Value.IntervalStartUtc.UtcDateTime),
        TimestampTz(Value.IntervalEndUtc.UtcDateTime),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Open },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.High },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Low },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Close },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Volume },
        Bigint(Value.TradeCount),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.PriceVolumeSum },
        Bigint(Value.FirstSourceSequence),
        Bigint(Value.LastSourceSequence),
        TimestampTz(Value.FirstMarketEventUtc.UtcDateTime),
        TimestampTz(Value.LastMarketEventUtc.UtcDateTime),
        TimestampTz(Value.CalculatedAtUtc.UtcDateTime),
        Integer((int)Value.SchemaVersion),
        Text(Value.CalculationVersion),
        Boolean(Value.IsComplete),
        Boolean(Value.IsValid)
    } : new object?[]
    {
        Value.MarketSeriesIdentity.Format(), Value.TimeFrame.ToString(), checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month),
        Value.IntervalEndUtc.UtcDateTime, Value.ObservationId.Value, Value.ContractId, Value.ValueDate,
        Value.IntervalStartUtc.UtcDateTime, Value.IntervalEndUtc.UtcDateTime, Value.Open, Value.High,
        Value.Low, Value.Close, Value.Volume, Value.TradeCount, Value.PriceVolumeSum,
        Value.FirstSourceSequence, Value.LastSourceSequence, Value.FirstMarketEventUtc.UtcDateTime,
        Value.LastMarketEventUtc.UtcDateTime, Value.CalculatedAtUtc.UtcDateTime,
        (int)Value.SchemaVersion, Value.CalculationVersion, Value.IsComplete, Value.IsValid
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalRawEodKey(string SeriesKey, int YearMonth, DateOnly ValueDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(SeriesKey),
        Integer(YearMonth),
        Date(ValueDate)
    } : new object?[] { SeriesKey, YearMonth, ValueDate };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalRawEodParameter(FuturesEodObservationReadModel Value) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(Value.MarketSeriesIdentity.Format()),
        Integer(checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month)),
        Date(Value.ValueDate),
        Text(Value.ContractId),
        TimestampTz(Value.SessionStartUtc.UtcDateTime),
        TimestampTz(Value.SessionEndUtc.UtcDateTime),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Open },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.High },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Low },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Close },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Volume },
        Bigint(Value.TradeCount),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.PriceVolumeSum },
        Uuid(Value.ObservationId.Value),
        Bigint(Value.FirstSourceSequence),
        Bigint(Value.LastSourceSequence),
        TimestampTz(Value.FirstMarketEventUtc.UtcDateTime),
        TimestampTz(Value.LastMarketEventUtc.UtcDateTime),
        Integer((int)Value.SchemaVersion),
        Boolean(Value.IsComplete),
        Boolean(Value.IsValid)
    } : new object?[]
    {
        Value.MarketSeriesIdentity.Format(), checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month), Value.ValueDate, Value.ContractId,
        Value.SessionStartUtc.UtcDateTime, Value.SessionEndUtc.UtcDateTime, Value.Open, Value.High,
        Value.Low, Value.Close, Value.Volume, Value.TradeCount, Value.PriceVolumeSum,
        Value.ObservationId.Value, Value.FirstSourceSequence, Value.LastSourceSequence,
        Value.FirstMarketEventUtc.UtcDateTime, Value.LastMarketEventUtc.UtcDateTime,
        (int)Value.SchemaVersion, Value.IsComplete, Value.IsValid
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalRawEodRangeKey(
    string SeriesKey,
    int YearMonth,
    DateOnly StartDate,
    DateOnly EndDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(SeriesKey),
        Integer(YearMonth),
        Date(StartDate),
        Date(EndDate)
    } : new object?[] { SeriesKey, YearMonth, StartDate, EndDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertEconomicCalendarCountryCode(int lookupId, string countryCode) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(lookupId),
        Text(countryCode)
    } : new object?[] { lookupId, countryCode };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertEconomicCalendarV2(string countryCode, int monthBucket, DateTime eventDate, string eventName, string? actual, string? forecast, string? prior, string? impact, string? unit, string? change, string? changePercentage, DateTime createdOn, string createdBy, Guid commandId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(countryCode),
        Integer(monthBucket),
        TimestampTz(eventDate),
        Text(eventName),
        Text(actual),
        Text(forecast),
        Text(prior),
        Text(impact),
        Text(unit),
        Text(change),
        Text(changePercentage),
        TimestampTz(createdOn),
        Text(createdBy),
        Uuid(commandId)
    } : new object?[] { countryCode, monthBucket, eventDate, eventName, actual, forecast, prior, impact, unit, change, changePercentage, createdOn, createdBy, commandId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesAdxSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double plusDI, double minusDI, double adxValue, string adx, string adxStrength, string? configurationId, Guid? observationId, DateTime? marketDataAsOf, long? sourceSequence, string? calculationVersion, string? calculationMethod, int? schemaVersion, bool? isValid, bool isWarm = false) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(periodLength),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = futuresPrice },
        Double(plusDI),
        Double(minusDI),
        Double(adxValue),
        Text(adx),
        Text(adxStrength),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Bigint(sourceSequence),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Boolean(isWarm)
    } : new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, plusDI, minusDI, adxValue, adx, adxStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesAtrSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double atrValue, double trueRange, string atr, string atrStrength, string? configurationId, Guid? observationId, DateTime? marketDataAsOf, long? sourceSequence, string? calculationVersion, string? calculationMethod, int? schemaVersion, bool? isValid, double? previousAtrValue, double? atrBaseline, double? atrRatio, bool isWarm) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(periodLength),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = futuresPrice },
        Double(atrValue),
        Double(trueRange),
        Text(atr),
        Text(atrStrength),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Bigint(sourceSequence),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Double(previousAtrValue),
        Double(atrBaseline),
        Double(atrRatio),
        Boolean(isWarm)
    } : new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, atrValue, trueRange, atr, atrStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, previousAtrValue, atrBaseline, atrRatio, isWarm };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime barDate, string barRateType, decimal barValue, double upTrendTrigger, double downTrendTrigger) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate),
        TimestampTz(barDate),
        Text(barRateType),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = barValue },
        Double(upTrendTrigger),
        Double(downTrendTrigger)
    } : new object?[] { contractId, symbol, valueDate, barDate, barRateType, barValue, upTrendTrigger, downTrendTrigger };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesBollingerBandSignal(
    string seriesKey, string timePeriod, string configurationId, int yearMonth,
    DateTime marketDataAsOf, Guid observationId, string contractId, DateOnly valueDate,
    decimal price, decimal? ema10Center, decimal? standardDeviation10, decimal? upper10,
    decimal? lower10, decimal? width10, decimal? position10, decimal? ema20Center,
    decimal? standardDeviation20, decimal? upper20, decimal? lower20, decimal? width20,
    decimal? position20, decimal? width20Baseline, decimal? width20Ratio, bool isWarm,
    long sourceSequence, DateTime calculatedAt, int schemaVersion,
    string calculationVersion, string calculationMethod, bool isValid) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(seriesKey),
        Text(timePeriod),
        Text(configurationId),
        Integer(yearMonth),
        TimestampTz(marketDataAsOf),
        Uuid(observationId),
        Text(contractId),
        Date(valueDate),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema10Center },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = standardDeviation10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = upper10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lower10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = position10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema20Center },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = standardDeviation20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = upper20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lower20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = position20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width20Baseline },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width20Ratio },
        Boolean(isWarm),
        Bigint(sourceSequence),
        TimestampTz(calculatedAt),
        Integer(schemaVersion),
        Text(calculationVersion),
        Text(calculationMethod),
        Boolean(isValid)
    } : new object?[]
    {
        seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId,
        contractId, valueDate, price, ema10Center, standardDeviation10, upper10, lower10,
        width10, position10, ema20Center, standardDeviation20, upper20, lower20, width20,
        position20, width20Baseline, width20Ratio, isWarm, sourceSequence, calculatedAt,
        schemaVersion, calculationVersion, calculationMethod, isValid
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesClosingPrice(string contractId, DateOnly valueDate, decimal closingPrice, DateTime createdOn, string createdBy) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closingPrice },
        TimestampTz(createdOn),
        Text(createdBy)
    } : new object?[] { contractId, valueDate, closingPrice, createdOn, createdBy };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesEmaSignal(
    string seriesKey, string timePeriod, string configurationId, int yearMonth,
    DateTime marketDataAsOf, Guid observationId, string contractId, DateOnly valueDate,
    decimal price, decimal? ema10, decimal? previousEma10, decimal? ema10Slope,
    decimal? ema20, decimal? previousEma20, decimal? ema20Slope,
    decimal? ema50, decimal? previousEma50, decimal? ema50Slope,
    decimal? ema200, decimal? previousEma200, decimal? ema200Slope, bool isWarm,
    long sourceSequence, DateTime calculatedAt, int schemaVersion,
    string calculationVersion, string calculationMethod, bool isValid) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(seriesKey),
        Text(timePeriod),
        Text(configurationId),
        Integer(yearMonth),
        TimestampTz(marketDataAsOf),
        Uuid(observationId),
        Text(contractId),
        Date(valueDate),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema10Slope },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema20Slope },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema50 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma50 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema50Slope },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema200 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma200 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema200Slope },
        Boolean(isWarm),
        Bigint(sourceSequence),
        TimestampTz(calculatedAt),
        Integer(schemaVersion),
        Text(calculationVersion),
        Text(calculationMethod),
        Boolean(isValid)
    } : new object?[]
    {
        seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId,
        contractId, valueDate, price, ema10, previousEma10, ema10Slope,
        ema20, previousEma20, ema20Slope, ema50, previousEma50, ema50Slope,
        ema200, previousEma200, ema200Slope, isWarm, sourceSequence, calculatedAt,
        schemaVersion, calculationVersion, calculationMethod, isValid
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize, decimal fiftyDMA, decimal twoHundredDMA) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate), Text(symbol), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice }, Bigint(volume), Double(dailyPercentChange), Double(dailyStdDev), Double(dailyStdDevAmount), Double(upperBand), Double(mean), Double(lowerBand), Text(marketDirection), Text(marketVolatility), Text(priceDirection), Text(priceVolatility), Double(marketDirectionIndicator), Integer(windowSize), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = fiftyDMA }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = twoHundredDMA }) : new object?[] { contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesEodDataByMonth(int yearMonth, string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize, decimal fiftyDMA, decimal twoHundredDMA) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Integer(yearMonth), Text(contractId), Date(valueDate), Text(symbol), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice }, Bigint(volume), Double(dailyPercentChange), Double(dailyStdDev), Double(dailyStdDevAmount), Double(upperBand), Double(mean), Double(lowerBand), Text(marketDirection), Text(marketVolatility), Text(priceDirection), Text(priceVolatility), Double(marketDirectionIndicator), Integer(windowSize), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = fiftyDMA }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = twoHundredDMA }) : new object?[] { yearMonth, contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesEodDataIndex(DateOnly valueDate, string contractId) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Date(valueDate), Text(contractId)) : new object?[] { valueDate, contractId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesIntraDayData(string contractId, DateOnly valueDate, long sequenceId, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate), Bigint(sequenceId), Text(symbol), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice }, Bigint(volume), Double(dailyPercentChange), Double(dailyStdDev), Double(dailyStdDevAmount), Double(upperBand), Double(mean), Double(lowerBand), Text(marketDirection), Text(marketVolatility), Text(priceDirection), Text(priceVolatility), Double(marketDirectionIndicator), Integer(windowSize)) : new object?[] { contractId, valueDate, sequenceId, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesItiSignal(string contractId, DateOnly valueDate, string timePeriod, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState, double bandLevel, double reversalLevel) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Bigint(sequenceId),
        TimestampTz(intrinsicTime),
        Integer(intrinsicTimeGroupId),
        Double(intrinsicTimeLength),
        Double(intrinsicPrice),
        Text(intrinsicTimeTrend),
        Text(intrinsicTimeMode),
        Double(trendPrice),
        Double(trendExtreme),
        Double(trendReversal),
        Double(trendDelta),
        Double(targetDelta),
        Double(lambda),
        Integer(tradingDays),
        Double(threshold),
        Double(upTrendTrigger),
        Double(downTrendTrigger),
        Text(tradeState),
        Double(bandLevel),
        Double(reversalLevel)
    } : new object?[] { contractId, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandLevel, reversalLevel };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesItiSignalByContractMonth(int yearMonth, string contractId, DateOnly valueDate, string timePeriod, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState, double bandLevel, double reversalLevel) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandLevel, reversalLevel };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesItiSignalIndex(DateOnly valueDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { valueDate, contractId };
}






[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesMacdSignal(
    string contractId,
    DateOnly valueDate,
    string timePeriod,
    int signalEmaPeriod,
    int fastEmaPeriod,
    int slowEmaPeriod,
    TimeOnly timestamp,
    decimal futuresPrice,
    double fastEma,
    double slowEma,
    double macdLine,
    double signalLine,
    double histogram,
    string macd,
    string macdStrength,
    string? configurationId,
    Guid? observationId,
    DateTime? marketDataAsOf,
    long? sourceSequence,
    string? calculationVersion,
    string? calculationMethod,
    int? schemaVersion,
    bool? isValid,
    bool isWarm,
    int observationCount) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(signalEmaPeriod),
        Integer(fastEmaPeriod),
        Integer(slowEmaPeriod),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = futuresPrice },
        Double(fastEma),
        Double(slowEma),
        Double(macdLine),
        Double(signalLine),
        Double(histogram),
        Text(macd),
        Text(macdStrength),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Bigint(sourceSequence),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Boolean(isWarm),
        Integer(observationCount)
    } : new object?[] { contractId, valueDate, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, timestamp, futuresPrice, fastEma, slowEma, macdLine, signalLine, histogram, macd, macdStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, isWarm, observationCount };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, double optionPrice, double bidPrice, double askPrice, int bidSize, int askSize, double impliedVolatility, double underlyingPrice, double delta, double gamma, double vega, double theta, double rho) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Bigint(tickId),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = tickTime },
        Double(optionPrice),
        Double(bidPrice),
        Double(askPrice),
        Integer(bidSize),
        Integer(askSize),
        Double(impliedVolatility),
        Double(underlyingPrice),
        Double(delta),
        Double(gamma),
        Double(vega),
        Double(theta),
        Double(rho)
    } : new object?[] { contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesRsiSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal price, decimal priceChange, decimal priceGain, decimal priceLoss, decimal averagePriceGain, decimal averagePriceLoss, double rs, double rsi, double rsiAverage, double rsiSlope, long sourceSequence, DateTime sourceEventTimestamp, string? configurationId, Guid? observationId, DateTime? marketDataAsOf, string? calculationVersion, string? calculationMethod, int? schemaVersion, bool? isValid, double? previousRsi, double? regimeSlope, bool isWarm) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(periodLength),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceChange },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceGain },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceLoss },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = averagePriceGain },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = averagePriceLoss },
        Double(rs),
        Double(rsi),
        Double(rsiAverage),
        Double(rsiSlope),
        Bigint(sourceSequence),
        TimestampTz(sourceEventTimestamp),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Double(previousRsi),
        Double(regimeSlope),
        Boolean(isWarm)
    } : new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, price, priceChange, priceGain, priceLoss, averagePriceGain, averagePriceLoss, rs, rsi, rsiAverage, rsiSlope, sourceSequence, sourceEventTimestamp, configurationId, observationId, marketDataAsOf, calculationVersion, calculationMethod, schemaVersion, isValid, previousRsi, regimeSlope, isWarm };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTdiSignal(
    string contractId,
    string timePeriod,
    string configurationId,
    DateOnly valueDate,
    TimeOnly timestamp,
    int schemaVersion,
    int rsiPeriod,
    int priceLinePeriod,
    int signalLinePeriod,
    int marketBasePeriod,
    int volatilityBandPeriod,
    double volatilityBandDeviation,
    decimal price,
    double rsi,
    double priceLine,
    double signalLine,
    double marketBaseLine,
    double upperVolatilityBand,
    double lowerVolatilityBand,
    double bandWidth,
    double priceSignalDivergence,
    string crossType,
    string marketState,
    string trendDirection,
    string trendStrength,
    long sourceSequence,
    DateTime sourceEventTimestamp) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Text(timePeriod), Text(configurationId), Date(valueDate), new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp }, Integer(schemaVersion), Integer(rsiPeriod), Integer(priceLinePeriod), Integer(signalLinePeriod), Integer(marketBasePeriod), Integer(volatilityBandPeriod), Double(volatilityBandDeviation), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price }, Double(rsi), Double(priceLine), Double(signalLine), Double(marketBaseLine), Double(upperVolatilityBand), Double(lowerVolatilityBand), Double(bandWidth), Double(priceSignalDivergence), Text(crossType), Text(marketState), Text(trendDirection), Text(trendStrength), Bigint(sourceSequence), TimestampTz(sourceEventTimestamp)) : new object?[]
    {
        contractId, timePeriod, configurationId, valueDate, timestamp, schemaVersion,
        rsiPeriod, priceLinePeriod, signalLinePeriod, marketBasePeriod, volatilityBandPeriod,
        volatilityBandDeviation, price, rsi, priceLine, signalLine, marketBaseLine,
        upperVolatilityBand, lowerVolatilityBand, bandWidth, priceSignalDivergence,
        crossType, marketState, trendDirection, trendStrength, sourceSequence, sourceEventTimestamp
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, price, size };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTickDataByTime(string contractId, DateOnly valueDate, TimeOnly tickTime, long tickId, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickTime, tickId, price, size };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTradeSignal(string contractId, DateOnly valueDate, string timePeriod, long sequenceId, TimeOnly timestamp, double mean, double stdDev, double futuresPrice, double priceChangePercent, double fundRiskPercent, double rsi, double rsiSlope, string trendType, string trendStrength, string tradeSignal, string tdi, string tdiStrength, double mdi, string mdiTrend, double mdiUpTrendLimit, double mdiDownTrendLimit, double upTrendingTrigger, double downTrendingTrigger, double entryTrigger, double exitTrigger, double trendDelta, double trendExtreme, double trendReversal, decimal fiftyDma, decimal twoHundredDma, string tradeExecuteState) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Bigint(sequenceId),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        Double(mean),
        Double(stdDev),
        Double(futuresPrice),
        Double(priceChangePercent),
        Double(fundRiskPercent),
        Double(rsi),
        Double(rsiSlope),
        Text(trendType),
        Text(trendStrength),
        Text(tradeSignal),
        Text(tdi),
        Text(tdiStrength),
        Double(mdi),
        Text(mdiTrend),
        Double(mdiUpTrendLimit),
        Double(mdiDownTrendLimit),
        Double(upTrendingTrigger),
        Double(downTrendingTrigger),
        Double(entryTrigger),
        Double(exitTrigger),
        Double(trendDelta),
        Double(trendExtreme),
        Double(trendReversal),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = fiftyDma },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = twoHundredDma },
        Text(tradeExecuteState)
    } : new object?[] { contractId, valueDate, timePeriod, sequenceId, timestamp, mean, stdDev, futuresPrice, priceChangePercent, fundRiskPercent, rsi, rsiSlope, trendType, trendStrength, tradeSignal, tdi, tdiStrength, mdi, mdiTrend, mdiUpTrendLimit, mdiDownTrendLimit, upTrendingTrigger, downTrendingTrigger, entryTrigger, exitTrigger, trendDelta, trendExtreme, trendReversal, fiftyDma, twoHundredDma, tradeExecuteState };
}


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



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesVwapSignal(
    string contractId, DateOnly valueDate, string configurationId,
    DateTime asOfUtc, long lastTradeOrdinal, DateTime sessionStartUtc, DateTime sessionEndUtc,
    decimal cumulativePriceVolume, long cumulativeVolume, long eligibleTradeCount,
    long rejectedTradeCount, decimal lastPrice, decimal? vwap, decimal? priceMinusVwap,
    decimal? priceToVwapPercent, long lastTradeSourceSequence, Guid streamEpochId,
    bool isWarm, bool isValid, string invalidReason, bool isTickExact,
    string calculationMethod, int schemaVersion, string calculationVersion) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(configurationId),
        TimestampTz(asOfUtc),
        Bigint(lastTradeOrdinal),
        TimestampTz(sessionStartUtc),
        TimestampTz(sessionEndUtc),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = cumulativePriceVolume },
        Bigint(cumulativeVolume),
        Bigint(eligibleTradeCount),
        Bigint(rejectedTradeCount),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lastPrice },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = vwap },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceMinusVwap },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceToVwapPercent },
        Bigint(lastTradeSourceSequence),
        Uuid(streamEpochId),
        Boolean(isWarm),
        Boolean(isValid),
        Text(invalidReason),
        Boolean(isTickExact),
        Text(calculationMethod),
        Integer(schemaVersion),
        Text(calculationVersion)
    } : new object?[]
    {
        contractId, valueDate, configurationId, asOfUtc, lastTradeOrdinal,
        sessionStartUtc, sessionEndUtc, cumulativePriceVolume, cumulativeVolume,
        eligibleTradeCount, rejectedTradeCount, lastPrice, vwap, priceMinusVwap,
        priceToVwapPercent, lastTradeSourceSequence, streamEpochId, isWarm, isValid,
        invalidReason, isTickExact, calculationMethod, schemaVersion, calculationVersion
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesVxTermStructureSignal(
    DateOnly valueDate, string configurationId, DateTime calculatedAt,
    long frontSourceSequence, long backSourceSequence,
    string frontContractId, DateOnly frontExpiry, decimal frontPrice,
    string backContractId, DateOnly backExpiry, decimal backPrice,
    decimal frontBackSpread, decimal frontBackRatio, decimal termStructurePercent,
    string termStructureState, decimal? priorFrontBackRatio,
    decimal? priorTermStructurePercent, DateTime frontSourceTimestamp,
    DateTime backSourceTimestamp, bool isWarm, bool isValid,
    int schemaVersion, string calculationVersion) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(valueDate),
        Text(configurationId),
        TimestampTz(calculatedAt),
        Bigint(frontSourceSequence),
        Bigint(backSourceSequence),
        Text(frontContractId),
        Date(frontExpiry),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = frontPrice },
        Text(backContractId),
        Date(backExpiry),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = backPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = frontBackSpread },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = frontBackRatio },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = termStructurePercent },
        Text(termStructureState),
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priorFrontBackRatio },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priorTermStructurePercent },
        TimestampTz(frontSourceTimestamp),
        TimestampTz(backSourceTimestamp),
        Boolean(isWarm),
        Boolean(isValid),
        Integer(schemaVersion),
        Text(calculationVersion)
    } : new object?[]
    {
        valueDate, configurationId, calculatedAt, frontSourceSequence,
        backSourceSequence, frontContractId, frontExpiry, frontPrice,
        backContractId, backExpiry, backPrice, frontBackSpread, frontBackRatio,
        termStructurePercent, termStructureState, priorFrontBackRatio,
        priorTermStructurePercent, frontSourceTimestamp, backSourceTimestamp,
        isWarm, isValid, schemaVersion, calculationVersion
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMarketDataProjectionMonth(string projectionName, int yearMonth) : IBindValue
{
    public object Bind() => new object?[] { projectionName, yearMonth };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMarketDataProjectionMutation(string projectionName, Guid mutationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId, startedOn };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKey, mutationId, startedOn };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMarketHoliday(string currencyType, DateOnly holidayDate, string description) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(currencyType),
        Date(holidayDate),
        Text(description)
    } : new object?[] { currencyType, holidayDate, description };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertRateOfReturn(string symbol, DateOnly valueDate, double rateOfReturn) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(symbol),
        Date(valueDate),
        Double(rateOfReturn)
    } : new object?[] { symbol, valueDate, rateOfReturn };
}



internal sealed class InsertTickQuoteData(
    object?[] values,
    TickQuoteScyllaBindValue? encoded = null)
    : IBindValue, TomasAI.IFM.Framework.Storage.ScyllaDb.IScyllaOwnedBindValues
{
    private object?[]? _values = values ?? throw new ArgumentNullException(nameof(values));

    /// <summary>Returns this one-time Scylla binding owner to the repository context.</summary>
    public object Bind() => this;

    /// <summary>Retains the encoded quote array until the Scylla driver finishes.</summary>
    public IDisposable? BindLifetime { get; } = encoded;

    /// <summary>Transfers the fresh quote insert values to one prepared-statement bind.</summary>
    public object?[] TakeValues()
        => Interlocked.Exchange(ref _values, null)
            ?? throw new InvalidOperationException("Tick quote parameters have already been bound.");
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTickTradeData(object?[] Values) : IBindValue
{
    public object Bind() => Values;
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeLiveFeed(int orderId, int tradeId, string tradeLiveFeedState) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(orderId),
        Integer(tradeId),
        Text(tradeLiveFeedState)
    } : new object?[] { orderId, tradeId, tradeLiveFeedState };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertVixFuturesContractIndex(int bucket, string contractId) : IBindValue
{
    public object Bind() => new object?[] { bucket, contractId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertVixFuturesEodData(
    string contractId,
    DateOnly valueDate,
    decimal openPrice,
    decimal highPrice,
    decimal lowPrice,
    decimal closePrice,
    long volume) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice },
        Bigint(volume)
    } : new object?[] { contractId, valueDate, openPrice, highPrice, lowPrice, closePrice, volume };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertYieldCurveRate(int id, DateOnly valueDate, double oneMonth, double twoMonth, double threeMonth, double sixMonth, double oneYear, double twoYear, double threeYear, double fiveYear, double sevenYear, double tenYear, double twentyYear, double thirtyYear) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(id),
        Date(valueDate),
        Double(oneMonth),
        Double(twoMonth),
        Double(threeMonth),
        Double(sixMonth),
        Double(oneYear),
        Double(twoYear),
        Double(threeYear),
        Double(fiveYear),
        Double(sevenYear),
        Double(tenYear),
        Double(twentyYear),
        Double(thirtyYear)
    } : new object?[] { id, valueDate, oneMonth, twoMonth, threeMonth, sixMonth, oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear, twentyYear, thirtyYear };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertYieldCurveRateYear(int lookupId, int rateYear) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(lookupId),
        Integer(rateYear)
    } : new object?[] { lookupId, rateYear };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MarkMarketDataProjectionScopeAtomicWriteV3(
    string projectionName,
    string scopeKey,
    Guid generation) : IBindValue
{
    public object Bind() => new object?[] { generation, projectionName, scopeKey };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeValueDateParameters(DateOnly ValueDate) : IBindValue
{
    public object Bind() => Values(Date(ValueDate));
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OptionTradeEvidenceParameters(object[] Items) : IBindValue
{
    public object Bind() => Items;
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OptionVolatilityParameters(object?[] Items) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Items.Select(ToParameter).ToArray() : Items;
    static NpgsqlParameter ToParameter(object? value) => value switch
    {
        string text => Text(text), int number => Integer(number), long number => Bigint(number),
        DateOnly date => Date(date), DateTime timestamp => TimestampTz(timestamp), byte[] payload => Bytea(payload),
        _ => throw new ArgumentException("Unsupported volatility parameter type.",nameof(value))
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RegisterMarketDataProjectionGuardOperationV3(
    string projectionName,
    string scopeKey,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, scopeKey };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RemoveMarketDataProjectionOperations(
    string projectionName,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RemoveMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName, scopeKey };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RestoreMarketDataProjectionState(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        completedOn,
        projectionName,
        generation,
        expectedActiveOperations
    };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize, decimal fiftyDMA, decimal twoHundredDMA) : IBindValue
{
    public object Bind() => new object?[] { openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA, contractId, valueDate, symbol };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateFuturesEodSessionStatistics(
    string contractId,
    DateOnly valueDate,
    string symbol,
    decimal openPrice,
    decimal highPrice,
    decimal lowPrice,
    long volume,
    double dailyPercentChange,
    string priceDirection) : IBindValue
{
    public object Bind() => new object?[]
    {
        openPrice,
        highPrice,
        lowPrice,
        volume,
        dailyPercentChange,
        priceDirection,
        contractId,
        valueDate,
        symbol
    };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionChainGreeks(string underlyingContractId, DateOnly expiryDate,
    FuturesOptionTickDataV2ReadModel tick) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Double(tick.ImpliedVolatility),
        Double(tick.UnderlyingPrice),
        Double(tick.Delta),
        Double(tick.Gamma),
        Double(tick.Vega),
        Double(tick.Theta),
        Double(tick.Rho),
        Text(underlyingContractId),
        Date(expiryDate),
        Date(tick.ValueDate),
        Text(tick.ContractId),
        Bigint(tick.TickId)
    } : new object?[] { tick.ImpliedVolatility, tick.UnderlyingPrice, tick.Delta,
        tick.Gamma, tick.Vega, tick.Theta, tick.Rho, underlyingContractId, expiryDate,
        tick.ValueDate, tick.ContractId, tick.TickId };
}



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionChainStatisticsParameters(
    string UnderlyingContractId, DateOnly ExpiryDate, DateOnly ValueDate, string ContractId,
    long? Volume, long? OpenInterest, DateOnly? VolumeValueDate, DateOnly? OpenInterestValueDate) : IBindValue
{
    public object Bind() => new object?[] { Volume, OpenInterest, VolumeValueDate, OpenInterestValueDate,
        UnderlyingContractId, ExpiryDate, ValueDate, ContractId };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateVixFuturesEodData(string contractId, DateOnly valueDate, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice },
        Bigint(volume),
        Text(contractId),
        Date(valueDate)
    } : new object?[] { openPrice, highPrice, lowPrice, closePrice, volume, contractId, valueDate };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertFuturesItiTimeFrameState(string contractId, string timePeriod, DateOnly calendarBucketStart, DateOnly timeFrameStartValueDate, DateOnly valueDate, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState, double bandAnchorPrice, double bandPercentage, double bandSize, double bandLevel, double reversalLevel) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Date(calendarBucketStart),
        Date(timeFrameStartValueDate),
        Date(valueDate),
        Bigint(sequenceId),
        TimestampTz(intrinsicTime),
        Integer(intrinsicTimeGroupId),
        Double(intrinsicTimeLength),
        Double(intrinsicPrice),
        Text(intrinsicTimeTrend),
        Text(intrinsicTimeMode),
        Double(trendPrice),
        Double(trendExtreme),
        Double(trendReversal),
        Double(trendDelta),
        Double(targetDelta),
        Double(lambda),
        Integer(tradingDays),
        Double(threshold),
        Double(upTrendTrigger),
        Double(downTrendTrigger),
        Text(tradeState),
        Double(bandAnchorPrice),
        Double(bandPercentage),
        Double(bandSize),
        Double(bandLevel),
        Double(reversalLevel)
    } : new object?[] { contractId, timePeriod, calendarBucketStart, timeFrameStartValueDate, valueDate, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandAnchorPrice, bandPercentage, bandSize, bandLevel, reversalLevel };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertFuturesOptionChainQuoteData(
    string underlyingContractId, DateOnly expiryDate, FuturesOptionTickDataV2ReadModel tick) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(underlyingContractId),
        Date(expiryDate),
        Date(tick.ValueDate),
        Text(tick.ContractId),
        Bigint(tick.TickId),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = tick.TickTime },
        Double(tick.OptionPrice),
        Double(tick.BidPrice),
        Double(tick.AskPrice),
        Integer(tick.BidSize),
        Integer(tick.AskSize),
        Double(tick.ImpliedVolatility),
        Double(tick.UnderlyingPrice),
        Double(tick.Delta),
        Double(tick.Gamma),
        Double(tick.Vega),
        Double(tick.Theta),
        Double(tick.Rho)
    } : new object?[] { underlyingContractId, expiryDate, tick.ValueDate,
        tick.ContractId, tick.TickId, tick.TickTime, tick.OptionPrice, tick.BidPrice,
        tick.AskPrice, tick.BidSize, tick.AskSize, tick.ImpliedVolatility,
        tick.UnderlyingPrice, tick.Delta, tick.Gamma, tick.Vega, tick.Theta, tick.Rho };
}



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



[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertMarketOutlookWorkingState(
    string contractId,
    DateOnly valueDate,
    long revision,
    DateTime updatedOn,
    string status,
    byte[] workingState) : IBindValue
{
    public object Bind() => new object?[]
    {
        contractId, valueDate, revision, updatedOn, status, workingState
    };
}



/// <summary>Binds an exact workflow/input revision preparation lookup.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeCompositionReadParameters(Guid WorkflowId, long InputRevision, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Uuid(WorkflowId), Bigint(InputRevision)) : new object[] { WorkflowId, InputRevision };
}



/// <summary>Binds an immutable accepted composition preparation.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeCompositionWriteParameters(Guid WorkflowId, long InputRevision, byte[] Payload, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Uuid(WorkflowId), Bigint(InputRevision), Bytea(Payload)) : new object[] { WorkflowId, InputRevision, Payload };
}



/// <summary>Typed canonical ITI observation query inputs.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeItiDayMode(string contractId, DateOnly valueDate, string mode, long afterSequenceId) : IBindValue
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with the element type.")]
    public object Bind() => new NpgsqlParameter[] { Text(contractId), Date(valueDate), Text(mode), Bigint(afterSequenceId) };
}


/// <summary>Identifies the exact dated timeframe state being removed.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeItiDeleteState(string contractId, string timePeriod, DateOnly bucketStart, DateOnly valueDate) : IBindValue
{
    public object Bind() => new NpgsqlParameter[] { Text(contractId), Text(timePeriod), Date(bucketStart), Date(valueDate) };
}



/// <summary>Typed canonical ITI observation query inputs.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeItiLatestTrendMode(string contractId, DateOnly valueDate, string trend, string mode) : IBindValue
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with the element type.")]
    public object Bind() => new NpgsqlParameter[] { Text(contractId), Date(valueDate), Text(trend), Text(mode) };
}



/// <summary>Typed canonical ITI observation query inputs.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeItiRange(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate) : IBindValue
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with the element type.")]
    public object Bind() => new NpgsqlParameter[] { new NpgsqlParameter<string[]> { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, TypedValue = [.. contractIds] }, Date(startDate), Date(endDate) };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeMarketDateRangeParameter(DateOnly StartDate, DateOnly EndDate) : IBindValue
{
    public object Bind() => new NpgsqlParameter[] { Date(StartDate),Date(EndDate) };
}



/// <summary>Binds the environment for current option-chain policy reads in PostgreSQL.</summary>
internal readonly record struct NativeOptionChainPublicationReadParameters(string Environment) : IBindValue
{
    public object Bind() => Values(Text(Environment));
}



/// <summary>Binds an immutable option-chain policy identity in PostgreSQL.</summary>
internal readonly record struct NativeOptionChainVersionReadParameters(Guid SetId, int Version) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Integer(Version));
}



/// <summary>Binds an immutable provider-evidence lookup without object-array type inference.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeOptionEvidenceReadParameters(string ContractId, DateOnly ValueDate, string SourceId, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Text(ContractId), Date(ValueDate), Text(SourceId)) : new object[] { ContractId, ValueDate, SourceId };
}



/// <summary>Binds the complete immutable evidence identity, digest and accepted payload.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeOptionEvidenceWriteParameters(string ContractId, DateOnly ValueDate, string SourceId, string Digest, byte[] Payload, bool PostgreSql) : IBindValue
{
    public object Bind() => PostgreSql ? Values(Text(ContractId), Date(ValueDate), Text(SourceId), Text(Digest), Bytea(Payload)) : new object[] { ContractId, ValueDate, SourceId, Digest, Payload };
}


[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeVixDateParameter(DateOnly ValueDate) : IBindValue
{
    public object Bind() => new NpgsqlParameter[] { Date(ValueDate) };
}

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalDataLoadAttemptParameter(Guid AttemptId) : IBindValue
    {
        public object Bind() => Values(Uuid(AttemptId));
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalDataLoadHashParameter(string Hash) : IBindValue
    {
        public object Bind() => Values(Text(Hash));
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct SaveHistoricalDataLoadParameter(HistoricalDataLoaderState State) : IBindValue
    {
        public object Bind() => Values(
            Uuid(State.DataLoadAttemptId),
            Text(State.RequestSha256),
            Smallint((short)State.Status),
            Smallint((short)State.Checkpoint.Stage),
            Text(State.Checkpoint.ProviderJobId),
            Text(State.Checkpoint.ProviderFileId),
            Bigint(State.Checkpoint.BatchOrdinal),
            Text(State.Checkpoint.SourcePosition),
            Text(State.ErrorMessage),
            TimestampTz(State.UpdatedAtUtc.UtcDateTime),
            Text(State.Manifest is null ? string.Empty : JsonSerializer.Serialize(State.Manifest)),
            Text(State.Audit is null ? string.Empty : JsonSerializer.Serialize(State.Audit)));
    }
