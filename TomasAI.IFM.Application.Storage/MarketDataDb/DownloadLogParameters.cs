using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

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
