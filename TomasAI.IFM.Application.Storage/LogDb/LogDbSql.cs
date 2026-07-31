using System;

namespace TomasAI.IFM.Application.Storage.LogDb;

/// <summary>
/// Contains SQL query strings for LogDb operations
/// </summary>
public static class LogDbSql
{
    /// <summary>
    /// SQL to get telemetry logs by date range
    /// </summary>
    public const string GetTelemtryLogsByDateRange = """
SELECT 
    timestamp AS "Timestamp",
    logLevel AS "LogLevel",
    message AS "Message",
    serviceId AS "ServiceId"
FROM 
    public.telemetry_log
WHERE 
    timestamp BETWEEN $1 AND $2
ORDER BY 
    SequenceId DESC;
""";

    /// <summary>
    /// SQL to insert a telemetry log entry
    /// </summary>
    public const string InsertTelemetryLog = """
INSERT INTO telemetry_log (
    sequenceId,
    timestamp,
    logLevel,
    message,
    serviceId
) VALUES (
    $1,
    $2,
    $3,
    $4,
    $5
);
""";
}
