using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.LogDb;

internal readonly record struct GetTelemetryLogsByDateRange(DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => Values(Timestamp(startDate), Timestamp(endDate));
}
internal readonly record struct InsertTelemetryLog(long logId, DateTime timestamp, string logLevel, string message, string serviceId) : IBindValue
{
    public object Bind() => Values(Bigint(logId), Timestamp(timestamp), Text(logLevel), Text(message), Text(serviceId));
}
