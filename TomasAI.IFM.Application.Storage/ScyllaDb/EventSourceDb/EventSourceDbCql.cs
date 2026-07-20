namespace TomasAI.IFM.Application.Storage.ScyllaDb.EventSourceDb;

internal class EventSourceDbCql
{
    public const string CreateEventSourceDbKeyspace = """
        CREATE KEYSPACE IF NOT EXISTS event_source_test_db
        WITH replication = {
            'class': 'SimpleStrategy',
            'replication_factor': 1
        };
    """;
    public const string CreateCommandLog = """
        CREATE TABLE IF NOT EXISTS command_log (
            commandId uuid,
            streamId text,
            actorName text,
            commandName text,
            commandTimestamp text,
            commandStatus text,
            commandData text,
            PRIMARY KEY (commandId)
        );
    """;

    public const string CreateEventLogTable = """
        CREATE TABLE IF NOT EXISTS event_log (
            eventStreamId bigint,
            eventVersion bigint,
            eventNameId int,
            eventData text,
            commandId uuid,
            eventTimestamp text,
            PRIMARY KEY ((eventStreamId, eventNameId), eventVersion)
        ) WITH CLUSTERING ORDER BY (eventVersion ASC);
    """;

    public const string CreateEventStreamIdTable = """
        CREATE TABLE IF NOT EXISTS event_stream_id (
            eventStreamId bigint,
            eventStream text,
            PRIMARY KEY (eventStream)
        );
    """;

    public const string CreateEventNameIdTable = """
        CREATE TABLE IF NOT EXISTS event_name_id (
            eventNameId int,
            eventName text,
            eventTypeName text,
            PRIMARY KEY (eventName)
        );
    """;

    public const string DeleteEventLog = """
        DELETE FROM event_log
        WHERE eventStreamId = :eventStreamId
        AND eventVersion = :eventVersion;
    """;

    public const string DeleteEventLogByStreamId = """
        DELETE FROM event_log
        WHERE eventStreamId = :streamId;
    """;

    public const string DeleteEventStreamById = """
        DELETE FROM event_stream_id
        WHERE eventStream = :eventStream;
    """;

    public const string DeleteEventNameId = """
        DELETE FROM event_name_id
        WHERE eventName = :eventName;
    """;

    public const string DeleteEventStreamId = """
        DELETE FROM event_stream_id
        WHERE eventStream = :eventStream;
    """;

    public const string GetCommandLog = """
        SELECT
            commandId AS "CommandId",
            streamId AS "StreamId",
            actorName AS "AggregateName",
            commandName AS "CommandName",
            commandTimestamp AS "CommandTimestamp",
            commandData AS "CommandData"
        FROM command_log
        WHERE commandId = :commandId;
    """;

    public const string InsertCommandLog = """
        INSERT INTO command_log (
            commandId,
            streamId,
            actorName,
            commandName,
            commandTimestamp,
            commandStatus,
            commandData
        ) VALUES (
            :commandId,
            :streamId,
            :aggregateName,
            :commandName,
            :commandTimestamp,
            :commandStatus,
            :commandData
        );
    """;

    public const string UpdateCommandLog = """
        UPDATE command_log
        SET commandStatus = :commandStatus,
            commandTimestamp = :updateTimestamp
        WHERE commandId = :commandId;
    """;

    public const string GetEventLogByEventStreamId = """
        SELECT
            eventStreamId AS "EventStreamId",
            eventName AS "EventName",
            eventTypeName AS "EventTypeName",
            eventVersion AS "EventVersion",
            eventData AS "EventData",
            commandId AS "CommandId",
            eventTimestamp AS "EventTimeStamp"
        FROM event_log
        WHERE eventStreamId = :eventStreamId
        ORDER BY eventVersion ASC;
    """;

    public const string GetEventLogByMaxEventVersion = """
        SELECT
            eventStreamId AS "EventStreamId",
            eventName AS "EventName",
            eventTypeName AS "EventTypeName",
            eventVersion AS "EventVersion",
            eventData AS "EventData",
            commandId AS "CommandId",
            eventTimestamp AS "EventTimeStamp"
        FROM event_log
        WHERE eventStreamId = :eventStreamId
        AND eventVersion >= :maxEventVersion
        ORDER BY eventVersion ASC;
    """;

    public const string GetEventLogByEventVersion = """
        SELECT
            eventStreamId AS "EventStreamId",
            eventName AS "EventName",
            eventTypeName AS "EventTypeName",
            eventVersion AS "EventVersion",
            eventData AS "EventData",
            commandId AS "CommandId",
            eventTimestamp AS "EventTimeStamp"
        FROM event_log
        WHERE eventVersion = :eventVersion
        ALLOW FILTERING;
    """;

    public const string GetEventLogLastNRange = """
        SELECT
            eventStreamId AS "EventStreamId",
            eventName AS "EventName",
            eventTypeName AS "EventTypeName",
            eventVersion AS "EventVersion",
            eventData AS "EventData",
            commandId AS "CommandId",
            eventTimestamp AS "EventTimeStamp"
        FROM event_log
        WHERE eventStreamId = :eventStreamId
        ORDER BY eventVersion DESC;
    """;

    public const string GetEventNameId = """
        SELECT
            eventNameId AS "EventNameId",
            eventName AS "EventName",
            eventTypeName AS "EventTypeName"
        FROM event_name_id
        WHERE eventName = :eventName;
    """;

    public const string GetEventStreamId = """
        SELECT
            eventStreamId AS "EventStreamId",
            eventStream AS "EventStream"
        FROM event_stream_id
        WHERE eventStream = :eventStream;
    """;

    public const string GetMaxEventVersion = """
        SELECT max(eventVersion) AS "MaxEventVersion"
        FROM event_log
        WHERE eventStreamId = :eventStreamId
        AND eventNameId = :snapshotEventNameId
        ALLOW FILTERING;
    """;

    public const string InsertEventLog = """
        INSERT INTO event_log (
            eventStreamId,
            eventNameId,
            eventName,
            eventTypeName,
            eventVersion,
            eventData,
            commandId,
            eventTimestamp
        ) VALUES (
            :eventStreamId,
            :eventNameId,
            :eventName,
            :eventTypeName,
            :eventVersion,
            :eventData,
            :commandId,
            :eventTimestamp
        ) IF NOT EXISTS;
    """;

    public const string UpdateEventLog = """
        UPDATE event_log
        SET eventData = :eventData,
            commandId = :commandId,
            eventTimestamp = :eventTimestamp
        WHERE eventStreamId = :eventStreamId
        AND eventVersion = :eventVersion;
    """;

    public const string InsertEventNameId = """
        INSERT INTO event_name_id (
            eventNameId,
            eventName,
            eventTypeName
        ) VALUES (
            :eventNameId,
            :eventName,
            :eventTypeName
        ) IF NOT EXISTS;
    """;

    public const string InsertEventStreamId = """
        INSERT INTO event_stream_id (
            eventStreamId,
            eventStream
        ) VALUES (
            :eventStreamId,
            :eventStream
        ) IF NOT EXISTS;
    """;
}
