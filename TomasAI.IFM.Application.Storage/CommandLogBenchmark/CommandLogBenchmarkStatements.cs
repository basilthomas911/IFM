namespace TomasAI.IFM.Application.Storage.CommandLogBenchmark;

/// <summary>Isolated ScyllaDB schema and CQL used only by comparison tests and benchmarks.</summary>
public static class CommandLogBenchmarkStatements
{
    public const string CreateScyllaTable = """
        CREATE TABLE IF NOT EXISTS command_log_benchmark (
            commandId uuid PRIMARY KEY,
            streamId text,
            actorName text,
            commandName text,
            commandTimestamp timestamp,
            commandData blob
        );
        """;

    public const string TryInsertScylla = """
        INSERT INTO command_log_benchmark (
            commandId, streamId, actorName, commandName, commandTimestamp, commandData)
        VALUES (
            :commandId, :streamId, :actorName, :commandName, :commandTimestamp, :commandData)
        IF NOT EXISTS;
        """;

    public const string GetScylla = """
        SELECT commandId, streamId, actorName, commandName, commandTimestamp, commandData
        FROM command_log_benchmark
        WHERE commandId = :commandId;
        """;

    public const string DeleteScylla = """
        DELETE FROM command_log_benchmark WHERE commandId = :commandId;
        """;

    public const string GetPostgres = """
        SELECT CommandId, StreamId, ActorName, CommandName,
               CommandTimestamp::timestamp, CommandData
        FROM command_log
        WHERE CommandId = $1;
        """;

    public const string DeletePostgres = """
        DELETE FROM command_log WHERE CommandId = $1;
        """;
}
