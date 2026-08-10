using System;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>
/// Contains SQL query strings for EventSourceDb operations
/// </summary>
public static class EventSourceDbSql
{
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
      cl.CommandId as "CommandId",
      cl.StreamId as "StreamId",
      coalesce(
        to_jsonb(cl) ->> 'aggregatename',
        to_jsonb(cl) ->> 'AggregateName',
        to_jsonb(cl) ->> 'actorname',
        to_jsonb(cl) ->> 'ActorName'
      ) as "AggregateName",
      cl.CommandName as "CommandName",
      cl.CommandTimestamp::timestamp as "CommandTimestamp",
      cl.CommandData as "CommandData"
    from command_log cl
    where cl.CommandId = $1
    """;

    public const string HasEventForCommand = """
    SELECT EXISTS (
        SELECT 1
        FROM event_log
        WHERE CommandId = $1
    );
    """;

    /// <summary>
    /// Represents the SQL statement used to insert a new record into the command log table.
    /// </summary>
    /// <remarks>The statement uses positional parameters ($1 through $7) for parameterized query execution.
    /// Ensure that parameter values are supplied in the correct order to match the columns: CommandId, StreamId,
    /// AggregateName, CommandName, CommandTimestamp, CommandStatus, and CommandData.</remarks>
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
/// Gets the last N events of one event type and restores chronological replay order.
/// </summary>
public const string GetEventLogLastNRangeByEventName = """
WITH last_event_range AS (
    SELECT
        el.eventStreamId,
        el.eventNameId,
        el.eventVersion,
        el.eventData,
        el.commandId,
        el.eventTimestamp
    FROM event_log el
    WHERE el.eventStreamId = $1
      AND el.eventNameId = $2
    ORDER BY el.eventVersion DESC
    LIMIT GREATEST($3, 0)
)
SELECT
    el.eventStreamId AS "EventStreamId",
    en.eventName AS "EventName",
    en.eventTypeName AS "EventTypeName",
    el.eventVersion AS "EventVersion",
    el.eventData AS "EventData",
    el.commandId AS "CommandId",
    el.eventTimestamp AS "EventTimeStamp"
FROM last_event_range el
JOIN event_name_id en ON el.eventNameId = en.eventNameId
ORDER BY el.eventVersion ASC;
""";

/// <summary>
/// Gets the latest snapshot and the last N matching events that follow it.
/// The inner range is selected newest-first so PostgreSQL can stop after N rows;
/// the outer query restores chronological replay order.
/// </summary>
public const string GetEventLogFromSnapshotLastNRange = """
WITH latest_snapshot AS (
    SELECT MAX(el.eventVersion) AS snapshotVersion
    FROM event_log el
    WHERE el.eventStreamId = $1
      AND el.eventNameId = $2
),
last_event_range AS (
    SELECT
        el.eventStreamId,
        el.eventNameId,
        el.eventVersion,
        el.eventData,
        el.commandId,
        el.eventTimestamp
    FROM event_log el
    CROSS JOIN latest_snapshot snapshot
    WHERE snapshot.snapshotVersion IS NOT NULL
      AND el.eventStreamId = $1
      AND el.eventNameId = $3
      AND el.eventVersion > snapshot.snapshotVersion
    ORDER BY el.eventVersion DESC
    LIMIT GREATEST($4, 0)
),
replay_range AS (
    SELECT
        el.eventStreamId,
        el.eventNameId,
        el.eventVersion,
        el.eventData,
        el.commandId,
        el.eventTimestamp
    FROM event_log el
    CROSS JOIN latest_snapshot snapshot
    WHERE snapshot.snapshotVersion IS NOT NULL
      AND el.eventStreamId = $1
      AND el.eventVersion = snapshot.snapshotVersion

    UNION ALL

    SELECT
        eventStreamId,
        eventNameId,
        eventVersion,
        eventData,
        commandId,
        eventTimestamp
    FROM last_event_range
)
SELECT
    el.eventStreamId AS "EventStreamId",
    en.eventName AS "EventName",
    en.eventTypeName AS "EventTypeName",
    el.eventVersion AS "EventVersion",
    el.eventData AS "EventData",
    el.commandId AS "CommandId",
    el.eventTimestamp AS "EventTimeStamp"
FROM replay_range el
JOIN event_name_id en ON el.eventNameId = en.eventNameId
ORDER BY el.eventVersion ASC;
""";

    /// <summary>
    /// Gets the durable state for one event/projector pair.
    /// </summary>
    public const string GetEventProjectorState = """
        SELECT
            EventId as "EventId",
            ActorName as "ActorName",
            ProjectorName as "ProjectorName",
            IsReplay as "IsReplay",
            AttemptNumber as "AttemptNumber",
            Outcome as "Outcome",
            Stage as "Stage",
            ErrorMessage as "ErrorMessage",
            CreatedTimestamp as "CreatedTimestamp",
            UpdatedTimestamp as "UpdatedTimestamp"
        FROM event_projector_state
        WHERE EventId = $1 AND ProjectorName = $2;
    """;

    const string EventProjectorExecutionStateColumns = """
            EventId as "EventId",
            ActorName as "ActorName",
            ProjectorName as "ProjectorName",
            IsReplay as "IsReplay",
            AttemptNumber as "AttemptNumber",
            Outcome as "Outcome",
            Stage as "Stage",
            ErrorMessage as "ErrorMessage",
            CreatedTimestamp as "CreatedTimestamp",
            UpdatedTimestamp as "UpdatedTimestamp",
            EventStreamId as "EventStreamId",
            SourceEventName as "SourceEventName",
            Revision as "Revision",
            ExecutionToken as "ExecutionToken",
            LeaseExpiresAtUtc as "LeaseExpiresAtUtc",
            RetryCount as "RetryCount",
            NextAttemptAtUtc as "NextAttemptAtUtc",
            LastErrorAtUtc as "LastErrorAtUtc",
            BlockedReason as "BlockedReason",
            LastCompletedStage as "LastCompletedStage",
            UpdatedAtUtc as "UpdatedAtUtc",
            BlockedStage as "BlockedStage"
        """;

    public const string TryCreateEventProjectorExecutionState = """
        INSERT INTO event_projector_state (
            EventId, ActorName, ProjectorName, IsReplay, AttemptNumber,
            Outcome, Stage, ErrorMessage, CreatedTimestamp, UpdatedTimestamp,
            EventStreamId, SourceEventName, UpdatedAtUtc
        )
        SELECT
            $1, $2, $3, $4, $5,
            $6, $7, $8, $9, $10,
            el.EventStreamId, en.EventName, $11
        FROM event_log el
        JOIN event_name_id en ON en.EventNameId = el.EventNameId
        WHERE el.EventVersion = $1
        ON CONFLICT (EventId, ProjectorName) DO NOTHING
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string GetEventProjectorExecutionState = """
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM event_projector_state
        WHERE EventId = $1 AND ProjectorName = $2;
        """;

    public const string TryClaimEventProjectorExecution = """
        UPDATE event_projector_state current_state
        SET ExecutionToken = $3,
            LeaseExpiresAtUtc = $4,
            Revision = Revision + 1,
            UpdatedAtUtc = $5,
            UpdatedTimestamp = $6
        WHERE current_state.EventId = $1
          AND current_state.ProjectorName = $2
          AND current_state.Outcome IN ('Processing', 'Retrying')
          AND (current_state.ExecutionToken IS NULL OR current_state.LeaseExpiresAtUtc IS NULL OR current_state.LeaseExpiresAtUtc <= $5)
          AND NOT EXISTS (
              SELECT 1
              FROM event_projector_state earlier
              WHERE earlier.ProjectorName = current_state.ProjectorName
                AND earlier.EventStreamId = current_state.EventStreamId
                AND earlier.EventId < current_state.EventId
                AND earlier.Outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded')
          )
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string HasEarlierUnresolvedEventProjectorExecution = """
        SELECT EXISTS (
            SELECT 1
            FROM event_projector_state current_state
            JOIN event_projector_state earlier
              ON earlier.ProjectorName = current_state.ProjectorName
             AND earlier.EventStreamId = current_state.EventStreamId
             AND earlier.EventId < current_state.EventId
            WHERE current_state.EventId = $1
              AND current_state.ProjectorName = $2
              AND earlier.Outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded')
        );
        """;

    public const string TryRenewEventProjectorExecution = """
        UPDATE event_projector_state
        SET LeaseExpiresAtUtc = $6,
            Revision = Revision + 1,
            UpdatedAtUtc = $5,
            UpdatedTimestamp = $7
        WHERE EventId = $1
          AND ProjectorName = $2
          AND ExecutionToken = $3
          AND Revision = $4
          AND LeaseExpiresAtUtc > $5
          AND Outcome IN ('Processing', 'Retrying')
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string TryReleaseEventProjectorExecution = """
        UPDATE event_projector_state
        SET ExecutionToken = NULL,
            LeaseExpiresAtUtc = NULL,
            Outcome = 'Retrying',
            RetryCount = $7,
            NextAttemptAtUtc = $8,
            LastErrorAtUtc = $9,
            ErrorMessage = $10,
            Revision = Revision + 1,
            UpdatedAtUtc = $11,
            UpdatedTimestamp = $12
        WHERE EventId = $1
          AND ProjectorName = $2
          AND ExecutionToken = $3
          AND Revision = $4
          AND Stage = $5
          AND LeaseExpiresAtUtc > $6
          AND Outcome IN ('Processing', 'Retrying')
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string TryTransitionEventProjectorExecution = """
        UPDATE event_projector_state
        SET Stage = $7,
            Outcome = $8,
            LastCompletedStage = $9,
            RetryCount = $10,
            NextAttemptAtUtc = $11,
            LastErrorAtUtc = $12,
            ErrorMessage = $13,
            BlockedReason = $14,
            Revision = Revision + 1,
            UpdatedAtUtc = $15,
            UpdatedTimestamp = $16
        WHERE EventId = $1
          AND ProjectorName = $2
          AND ExecutionToken = $3
          AND Revision = $4
          AND Stage = $5
          AND LeaseExpiresAtUtc > $6
          AND Outcome IN ('Processing', 'Retrying')
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string TryTerminalizeEventProjectorExecution = """
        UPDATE event_projector_state
        SET Stage = 'Completed',
            BlockedStage = CASE WHEN $7 = 'Failed' THEN Stage ELSE 'None' END,
            Outcome = $7,
            LastCompletedStage = $8,
            RetryCount = $9,
            NextAttemptAtUtc = NULL,
            LastErrorAtUtc = $10,
            ErrorMessage = $11,
            BlockedReason = $12,
            ExecutionToken = NULL,
            LeaseExpiresAtUtc = NULL,
            Revision = Revision + 1,
            UpdatedAtUtc = $13,
            UpdatedTimestamp = $14
        WHERE EventId = $1
          AND ProjectorName = $2
          AND ExecutionToken = $3
          AND Revision = $4
          AND Stage = $5
          AND LeaseExpiresAtUtc > $6
          AND Outcome IN ('Processing', 'Retrying')
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string TryTransitionEventProjectorExecutionWithOutbox = """
        WITH transitioned AS (
            UPDATE event_projector_state
            SET Stage = $7,
                Outcome = $8,
                LastCompletedStage = $9,
                RetryCount = $10,
                NextAttemptAtUtc = $11,
                LastErrorAtUtc = $12,
                ErrorMessage = $13,
                BlockedReason = $14,
                Revision = Revision + 1,
                UpdatedAtUtc = $15,
                UpdatedTimestamp = $16
            WHERE EventId = $1
              AND ProjectorName = $2
              AND ExecutionToken = $3
              AND Revision = $4
              AND Stage = $5
              AND LeaseExpiresAtUtc > $6
              AND Outcome IN ('Processing', 'Retrying')
            RETURNING *
        ), staged AS (
            INSERT INTO event_projector_outbox (
                ProjectorName, EventId, EffectKind, MessageId, EventTypeName,
                EventPayload, Status, AttemptCount, NextAttemptAtUtc, CreatedAtUtc, LastError)
            SELECT ProjectorName, EventId, $17, $18, $19,
                   $20, 'Pending', 0, $21, $21, ''
            FROM transitioned
            ON CONFLICT (ProjectorName, EventId, EffectKind) DO NOTHING
        )
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM transitioned;
        """;

    public const string TryTerminalizeEventProjectorExecutionWithOutbox = """
        WITH transitioned AS (
            UPDATE event_projector_state
            SET Stage = 'Completed',
                BlockedStage = CASE WHEN $7 = 'Failed' THEN Stage ELSE 'None' END,
                Outcome = $7,
                LastCompletedStage = $8,
                RetryCount = $9,
                NextAttemptAtUtc = NULL,
                LastErrorAtUtc = $10,
                ErrorMessage = $11,
                BlockedReason = $12,
                ExecutionToken = NULL,
                LeaseExpiresAtUtc = NULL,
                Revision = Revision + 1,
                UpdatedAtUtc = $13,
                UpdatedTimestamp = $14
            WHERE EventId = $1
              AND ProjectorName = $2
              AND ExecutionToken = $3
              AND Revision = $4
              AND Stage = $5
              AND LeaseExpiresAtUtc > $6
              AND Outcome IN ('Processing', 'Retrying')
            RETURNING *
        ), staged AS (
            INSERT INTO event_projector_outbox (
                ProjectorName, EventId, EffectKind, MessageId, EventTypeName,
                EventPayload, Status, AttemptCount, NextAttemptAtUtc, CreatedAtUtc, LastError)
            SELECT ProjectorName, EventId, $15, $16, $17,
                   $18, 'Pending', 0, $19, $19, ''
            FROM transitioned
            ON CONFLICT (ProjectorName, EventId, EffectKind) DO NOTHING
        )
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM transitioned;
        """;

    public const string ClaimEventProjectorOutbox = """
        WITH candidates AS (
            SELECT ProjectorName, EventId, EffectKind
            FROM event_projector_outbox
            WHERE ProjectorName = $1
              AND (
                    (Status IN ('Pending', 'Retrying') AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= $4))
                 OR (Status = 'Publishing' AND DispatchLeaseExpiresAtUtc <= $4)
              )
            ORDER BY CreatedAtUtc, EventId, EffectKind
            LIMIT $5
            FOR UPDATE SKIP LOCKED
        )
        UPDATE event_projector_outbox o
        SET Status = 'Publishing',
            AttemptCount = o.AttemptCount + 1,
            DispatchToken = $2,
            DispatchLeaseExpiresAtUtc = $3,
            NextAttemptAtUtc = NULL
        FROM candidates c
        WHERE o.ProjectorName = c.ProjectorName
          AND o.EventId = c.EventId
          AND o.EffectKind = c.EffectKind
        RETURNING
            o.ProjectorName as "ProjectorName",
            o.EventId as "EventId",
            o.EffectKind as "EffectKind",
            o.MessageId as "MessageId",
            o.EventTypeName as "EventTypeName",
            o.EventPayload as "EventPayload",
            o.Status as "Status",
            o.AttemptCount as "AttemptCount",
            o.NextAttemptAtUtc as "NextAttemptAtUtc",
            o.CreatedAtUtc as "CreatedAtUtc",
            o.PublishedAtUtc as "PublishedAtUtc",
            o.LastError as "LastError",
            o.DispatchToken as "DispatchToken",
            o.DispatchLeaseExpiresAtUtc as "DispatchLeaseExpiresAtUtc";
        """;

    public const string MarkEventProjectorOutboxPublished = """
        UPDATE event_projector_outbox
        SET Status = 'Published',
            PublishedAtUtc = $6,
            DispatchToken = NULL,
            DispatchLeaseExpiresAtUtc = NULL,
            NextAttemptAtUtc = NULL,
            LastError = ''
        WHERE ProjectorName = $1
          AND EventId = $2
          AND EffectKind = $3
          AND DispatchToken = $4
          AND Status = 'Publishing'
          AND DispatchLeaseExpiresAtUtc > $5;
        """;

    public const string ReleaseEventProjectorOutbox = """
        UPDATE event_projector_outbox
        SET Status = $6,
            NextAttemptAtUtc = $7,
            LastError = $8,
            DispatchToken = NULL,
            DispatchLeaseExpiresAtUtc = NULL
        WHERE ProjectorName = $1
          AND EventId = $2
          AND EffectKind = $3
          AND DispatchToken = $4
          AND Status = 'Publishing'
          AND DispatchLeaseExpiresAtUtc > $5;
        """;

    public const string GetEventProjectorOperationalStatePage = """
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM event_projector_state
        WHERE ProjectorName = $1
          AND EventId > $3
          AND (
                ($2 = 'Pending' AND Outcome IN ('Processing', 'Retrying') AND BlockedReason = '')
             OR ($2 = 'Failed' AND Outcome = 'Failed' AND BlockedReason = '')
             OR ($2 = 'Blocked' AND BlockedReason <> '')
          )
        ORDER BY EventId
        LIMIT $4;
        """;

    public const string GetEventProjectorOperationalSnapshot = """
        SELECT
            COUNT(*) FILTER (WHERE Outcome IN ('Processing', 'Retrying')) AS "PendingCount",
            MIN(UpdatedAtUtc) FILTER (WHERE Outcome IN ('Processing', 'Retrying')) AS "OldestPendingAtUtc",
            COUNT(*) FILTER (WHERE BlockedReason <> '') AS "BlockedCount",
            COUNT(*) FILTER (WHERE Outcome = 'Failed') AS "TerminalFailedCount",
            COUNT(*) FILTER (
                WHERE Outcome IN ('Processing', 'Retrying')
                  AND ExecutionToken IS NOT NULL
                  AND LeaseExpiresAtUtc <= $2) AS "ExpiredLeaseCount",
            (SELECT COUNT(*)
             FROM event_projector_outbox outbox
             WHERE outbox.ProjectorName = $1
               AND outbox.Status IN ('Pending', 'Retrying', 'Publishing')) AS "OutboxPendingCount",
            (SELECT MIN(outbox.CreatedAtUtc)
             FROM event_projector_outbox outbox
             WHERE outbox.ProjectorName = $1
               AND outbox.Status IN ('Pending', 'Retrying', 'Publishing')) AS "OldestOutboxPendingAtUtc",
            (SELECT COUNT(*)
             FROM event_projector_outbox outbox
             WHERE outbox.ProjectorName = $1
               AND outbox.Status = 'Retrying') AS "OutboxRetryCount"
        FROM event_projector_state
        WHERE ProjectorName = $1;
        """;

    public const string TryRetryEventProjectorExecution = """
        UPDATE event_projector_state
        SET Stage = BlockedStage,
            BlockedStage = 'None',
            Outcome = 'Retrying',
            AttemptNumber = 0,
            RetryCount = 0,
            NextAttemptAtUtc = NULL,
            LastErrorAtUtc = NULL,
            ErrorMessage = '',
            BlockedReason = '',
            ExecutionToken = NULL,
            LeaseExpiresAtUtc = NULL,
            Revision = Revision + 1,
            UpdatedAtUtc = $3,
            UpdatedTimestamp = $4
        WHERE EventId = $1
          AND ProjectorName = $2
          AND Stage = 'Completed'
          AND BlockedStage NOT IN ('None', 'Completed')
          AND (Outcome = 'Failed' OR (Outcome = 'Superseded' AND BlockedReason LIKE 'operator-skip:%'))
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string TrySkipEventProjectorExecution = """
        UPDATE event_projector_state
        SET Stage = 'Completed',
            BlockedStage = Stage,
            Outcome = 'Superseded',
            NextAttemptAtUtc = NULL,
            LastErrorAtUtc = $4,
            ErrorMessage = $3,
            BlockedReason = 'operator-skip:' || $3,
            ExecutionToken = NULL,
            LeaseExpiresAtUtc = NULL,
            Revision = Revision + 1,
            UpdatedAtUtc = $4,
            UpdatedTimestamp = $5
        WHERE EventId = $1
          AND ProjectorName = $2
          AND Outcome NOT IN ('Completed', 'Superseded', 'AlreadyCompleted')
        RETURNING
        """ + EventProjectorExecutionStateColumns + ";";

    public const string GetEventProjectorRecoveryPage = """
        SELECT
            el.EventStreamId as "EventStreamId",
            en.EventName as "EventName",
            en.EventTypeName as "EventTypeName",
            el.EventVersion as "EventVersion",
            el.EventData as "EventData",
            el.CommandId as "CommandId",
            el.EventTimestamp as "EventTimestamp",
            eps.EventId as "EventId",
            eps.ActorName as "ActorName",
            eps.ProjectorName as "ProjectorName",
            eps.IsReplay as "IsReplay",
            eps.AttemptNumber as "AttemptNumber",
            eps.Outcome as "Outcome",
            eps.Stage as "Stage",
            eps.ErrorMessage as "ErrorMessage",
            eps.CreatedTimestamp as "CreatedTimestamp",
            eps.UpdatedTimestamp as "UpdatedTimestamp",
            eps.EventStreamId as "StateEventStreamId",
            eps.SourceEventName as "SourceEventName",
            eps.Revision as "Revision",
            eps.ExecutionToken as "ExecutionToken",
            eps.LeaseExpiresAtUtc as "LeaseExpiresAtUtc",
            eps.RetryCount as "RetryCount",
            eps.NextAttemptAtUtc as "NextAttemptAtUtc",
            eps.LastErrorAtUtc as "LastErrorAtUtc",
            eps.BlockedReason as "BlockedReason",
            eps.LastCompletedStage as "LastCompletedStage",
            eps.UpdatedAtUtc as "UpdatedAtUtc"
        FROM event_projector_state eps
        JOIN event_log el ON el.EventVersion = eps.EventId
        JOIN event_name_id en ON en.EventNameId = el.EventNameId
        WHERE eps.ProjectorName = $1
          AND en.EventName = ANY(string_to_array($2, ','))
          AND eps.Outcome IN ('Processing', 'Retrying')
          AND eps.EventId > $3
          AND (eps.NextAttemptAtUtc IS NULL OR eps.NextAttemptAtUtc <= $4)
          AND (eps.ExecutionToken IS NULL OR eps.LeaseExpiresAtUtc IS NULL OR eps.LeaseExpiresAtUtc <= $4)
        ORDER BY eps.EventId
        LIMIT $5;
        """;

    public const string TryInsertCommandLog = """
        WITH inserted AS (
            INSERT INTO command_log (
                CommandId,
                StreamId,
                ActorName,
                CommandName,
                CommandTimestamp,
                CommandStatus,
                CommandData
            ) VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT (CommandId) DO NOTHING
            RETURNING CommandId
        )
        SELECT EXISTS (SELECT 1 FROM inserted);
    """;

    /// <summary>
    /// Gets source events that have an explicit projector state eligible for recovery.
    /// Event-log rows without projector state and terminal completed or failed states are deliberately excluded.
    /// </summary>
    public const string GetUncompletedEventProjectorEvents = """
        SELECT
            el.eventStreamId as "EventStreamId",
            en.eventName as "EventName",
            en.eventTypeName as "EventTypeName",
            el.eventVersion as "EventVersion",
            el.eventData as "EventData",
            el.commandId as "CommandId",
            el.eventTimestamp as "EventTimestamp"
        FROM event_log el
        JOIN event_name_id en ON el.eventNameId = en.eventNameId
        JOIN event_projector_state eps
          ON eps.EventId = el.eventVersion
         AND eps.ProjectorName = $1
        WHERE en.eventName = ANY(string_to_array($2, ','))
          AND eps.Outcome IN ('Processing', 'Retrying')
        ORDER BY el.eventVersion;
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
  AND e.eventTypeName = $2
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
    /// Inserts or updates durable state for a single event/projector pair.
    /// </summary>
    public const string UpsertEventProjectorState = """
        INSERT INTO event_projector_state (
            EventId,
            ActorName,
            ProjectorName,
            IsReplay,
            AttemptNumber,
            Outcome,
            Stage,
            ErrorMessage,
            CreatedTimestamp,
            UpdatedTimestamp,
            EventStreamId,
            SourceEventName,
            UpdatedAtUtc
        )
        SELECT
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7,
            $8,
            $9,
            $10,
            el.EventStreamId,
            en.EventName,
            CURRENT_TIMESTAMP
        FROM event_log el
        JOIN event_name_id en ON en.EventNameId = el.EventNameId
        WHERE el.EventVersion = $1
        ON CONFLICT (EventId, ProjectorName) DO UPDATE SET
            ActorName = EXCLUDED.ActorName,
            IsReplay = EXCLUDED.IsReplay,
            AttemptNumber = EXCLUDED.AttemptNumber,
            Outcome = EXCLUDED.Outcome,
            Stage = EXCLUDED.Stage,
            ErrorMessage = EXCLUDED.ErrorMessage,
            UpdatedTimestamp = EXCLUDED.UpdatedTimestamp,
            EventStreamId = COALESCE(event_projector_state.EventStreamId, EXCLUDED.EventStreamId),
            SourceEventName = CASE
                WHEN event_projector_state.SourceEventName = '' THEN EXCLUDED.SourceEventName
                ELSE event_projector_state.SourceEventName
            END,
            UpdatedAtUtc = CURRENT_TIMESTAMP;
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
