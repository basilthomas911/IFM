namespace TomasAI.IFM.Application.Storage.LogDb.Schema;

internal static class LogSchemaSql
{
    public const string CreateTelemetryLogTable = """
    CREATE TABLE IF NOT EXISTS public.telemetry_log (
    sequenceId BIGINT,
    timestamp TIMESTAMP,
    logLevel VARCHAR(50),
    message TEXT,
    serviceId VARCHAR(100),
    PRIMARY KEY (sequenceId, timestamp)
    );
    """;
}
