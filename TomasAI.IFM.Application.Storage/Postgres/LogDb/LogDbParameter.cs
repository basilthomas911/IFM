using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.Postgres.LogDb;

internal readonly record struct GetTelemetryLogsByDateRange(DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => new { startDate, endDate };
}
internal readonly record struct InsertTelemetryLog(long LogId, DateTime Timestamp, string LogLevel, string Message, string ServiceId) : IBindValue
{
    public object Bind() => new { LogId, Timestamp, LogLevel, Message, ServiceId };
}
