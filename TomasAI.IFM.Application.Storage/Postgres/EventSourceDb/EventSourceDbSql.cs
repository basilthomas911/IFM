using System;

namespace TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

/// <summary>
/// Contains SQL query strings for EventSourceDb operations
/// </summary>
public static class EventSourceDbSql
{
    public const string CreateCommandLog = """
        create table if not exists command_log (
            CommandId uuid primary key,
            StreamId text  not null,
            ActorName text not null,
            CommandName varchar(255) not null,
            CommandTimestamp text not null,
            CommandStatus varchar(50) not null,
            CommandData text not null
        );
    """;
    /// <summary>
    /// SQL to delete an event log by event version
    /// </summary>
    public const string DeleteEventLog = """
        delete from event_log 
        where eventVersion = $1
        """;

    /// <summary>
    /// Represents the SQL statement used to delete all records from the event log for a specified event stream ID.
    /// </summary>
    /// <remarks>The parameter $1 in the statement should be replaced with the desired event stream identifier
    /// when executing the query.</remarks>
    public const string DeleteEventLogByStreamId = """
        delete from event_log 
        where eventStreamId = $1
        """;

    /// <summary>
    /// Represents the SQL statement used to delete an event stream by its unique identifier.
    /// </summary>
    /// <remarks>This constant can be used to execute a parameterized SQL command that removes a record from
    /// the event_stream_id table where the eventStreamId matches the specified value. The parameter $1 should be
    /// replaced with the desired event stream identifier when executing the statement.</remarks>
    public const string DeleteEventStreamById = """
        DELETE FROM event_stream_id
        WHERE eventStreamId = $1;
        """;

    /// <summary>
    /// SQL to delete an event name ID
    /// </summary>
    public const string DeleteEventNameId = """
DELETE FROM event_name_id 
WHERE EventName = $1 
AND EventTypeName = $2;
""";

    /// <summary>
    /// SQL to delete an event stream ID
    /// </summary>
    public const string DeleteEventStreamId = """
DELETE FROM event_stream_id WHERE EventStream = $1;
""";

    /// <summary>
    /// SQL to get command log by command ID
    /// </summary>
    public const string GetCommandLog = """
    select
      CommandId as "CommandId",
      StreamId as "StreamId",
      ActorName as "ActorName",
      CommandName as "CommandName",
      CommandTimestamp as "CommandTimestamp",
      CommandStatus as "CommandStatus",
      CommandData as "CommandData"
    from public.fn_get_command_log($1)
    """;

    /// <summary>
    /// Represents the SQL statement used to insert a new record into the command log table.
    /// </summary>
    /// <remarks>The statement uses positional parameters ($1 through $7) for parameterized query execution.
    /// Ensure that parameter values are supplied in the correct order to match the columns: CommandId, StreamId,
    /// ActorName, CommandName, CommandTimestamp, CommandStatus, and CommandData.</remarks>
    public const string InsertCommandLog = """
        insert into command_log (
            CommandId,
            StreamId,
            ActorName,
            CommandName,
            CommandTimestamp,
            CommandStatus,
            CommandData
        ) values (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7
        );
    """;

    /// <summary>
    /// Represents the SQL statement used to update the status of a command in the command log table.
    /// </summary>
    /// <remarks>The SQL statement uses positional parameters for the command ID and the new status value.
    /// Ensure that parameter values are supplied in the correct order when executing this command.</remarks>
    public const string UpdateCommandLog = """
        update command_log
        set CommandStatus = $2,
            CommandTimestamp = $3
        where CommandId = $1;
    """;

    /// <summary>
    /// SQL to get event log by event stream ID
    /// </summary>
    public const string GetEventLogByEventStreamId = """
SELECT
        el.eventStreamId as "EventStreamId",
        en.eventName as "EventName",
  en.eventTypeName as "EventTypeName",
        el.eventVersion as "EventVersion",
        el.eventData as "EventData",
        el.commandId as "CommandId",
        el.eventTimestamp as "EventTimeStamp"
    FROM
        event_log el JOIN event_name_id en ON el.eventNameId = en.eventNameId
    WHERE
        el.eventStreamId = $1
    ORDER BY
        el.eventVersion;
""";

    /// <summary>
    /// SQL to get event log by max event version
    /// </summary>
 public const string GetEventLogByMaxEventVersion = """
    SELECT
      el.eventStreamId as "EventStreamId",
      en.eventName as "EventName",
      en.eventTypeName as "EventTypeName",
      el.eventVersion as "EventVersion",
      el.eventData as "EventData",
      el.commandId as "CommandId",
      el.eventTimestamp as "EventTimeStamp"
    FROM
      event_log el JOIN event_name_id en ON el.eventNameId = en.eventNameId
    WHERE
      el.eventVersion >= $2 and el.eventStreamId = $1
    ORDER BY
      el.eventVersion;
    """;

public const string GetEventLogByEventVersion = """
    SELECT
      el.eventStreamId as "EventStreamId",
      en.eventName as "EventName",
      en.eventTypeName as "EventTypeName",
      el.eventVersion as "EventVersion",
      el.eventData as "EventData",
      el.commandId as "CommandId",
      el.eventTimestamp as "EventTimeStamp"
    FROM
      event_log el JOIN event_name_id en ON el.eventNameId = en.eventNameId
    WHERE
      el.eventVersion = $1
    ORDER BY
      el.eventVersion;
    """;

/// <summary>
/// SQL to get last N events from event log
/// </summary>
public const string GetEventLogLastNRange = """
SELECT
        el.eventStreamId as "EventStreamId",
        en.eventName as "EventName",
  en.eventTypeName as "EventTypeName",
        el.eventVersion as "EventVersion",
        el.eventData as "EventData",
        el.commandId as "CommandId",
        el.eventTimestamp as "EventTimeStamp"
    FROM
        event_log el JOIN event_name_id en ON el.eventNameId = en.eventNameId
    WHERE
        el.eventStreamId = $1
    ORDER BY
        el.eventVersion DESC;
""";

    /// <summary>
    /// SQL to get event name ID by event name
    /// </summary>
    public const string GetEventNameId = """
SELECT
  e.eventNameId as "EventNameId",
  e.eventName as "EventName",
  e.eventTypeName as "EventTypeName"
FROM
  event_name_id e
WHERE
  e.eventName = $1
ORDER BY
  e.eventNameId;
""";

    /// <summary>
    /// SQL to get event stream ID by event stream
    /// </summary>
    public const string GetEventStreamId = """
SELECT
  e.eventStreamId as "EventStreamId",
  e.eventStream as "EventStream"
FROM
  event_stream_id e
WHERE
  e.eventStream = $1;
""";

    /// <summary>
    /// SQL to get maximum event version
    /// </summary>
    public const string GetMaxEventVersion = """
SELECT max(el.eventVersion) as "MaxEventVersion"
from event_log el 
where el.eventStreamId = $1
and el.eventNameId = $2
""";

/// <summary>
/// SQL to insert an event log
/// </summary>
public const string InsertEventLog = """
    INSERT INTO event_log (
            EventStreamId,
            EventNameId,
            EventData,
            CommandId,
            EventTimestamp
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5
        ) RETURNING eventVersion;
    """;

public const string UpdateEventLog = """
    UPDATE event_log SET
        EventData = $1,
        CommandId = $2,
        EventTimestamp = $3
    WHERE
        EventStreamId = $4 AND
        EventNameId = $5 AND
        EventVersion = $6
    RETURNING eventVersion;
    """;

    /// <summary>
    /// SQL to insert an event name ID
    /// </summary>
    public const string InsertEventNameId = """
INSERT INTO event_name_id (
  EventName,
  EventTypeName
) VALUES (
  $1,
  $2
) RETURNING eventNameId;
""";

    /// <summary>
    /// SQL to insert an event stream ID
    /// </summary>
    public const string InsertEventStreamId = """
INSERT INTO event_stream_id (
    EventStream
) VALUES (
    $1
) returning eventStreamId;
""";
}