

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>
/// Contains SQL query strings for EventSourceDb operations
/// </summary>
public static class EventSourceDbSql
{
    /// <summary>Checks a named resident stream without replaying source payloads.</summary>
    public const string GetResidentStreamHead = "SELECT COALESCE((SELECT CurrentVersion FROM event_stream_id WHERE EventStream=$1),0);";
    internal const string ReadConcernCheckpointWindow = """
        WITH streams AS MATERIALIZED (
            SELECT EventStreamId, CurrentVersion, EventStream=$1 AS is_concern
            FROM event_stream_id WHERE EventStream IN ($1,$2)
        ), heads AS MATERIALIZED (
            SELECT coalesce(max(CurrentVersion) FILTER (WHERE is_concern),0) AS concern_head,
                   coalesce(max(CurrentVersion) FILTER (WHERE NOT is_concern),0) AS legacy_head
            FROM streams
        ), checkpoint AS MATERIALIZED (
            SELECT max(latest.StreamVersion) AS stream_version
            FROM streams s CROSS JOIN event_name_id n
            CROSS JOIN LATERAL (
                SELECT e.StreamVersion FROM event_log e
                WHERE e.EventStreamId=s.EventStreamId AND e.EventNameId=n.EventNameId
                  AND e.StreamVersion>1 AND e.StreamVersion<=s.CurrentVersion
                ORDER BY e.EventVersion DESC LIMIT 1
            ) latest
            WHERE $4 AND s.is_concern AND n.EventTypeName=ANY($3)
        ), selected AS (
            SELECT NOT s.is_concern AS is_legacy,e.EventVersion,e.EventNameId,e.StreamVersion,e.EventPayload, e.MessageOriginJson AS MessageOriginJson
        FROM streams s CROSS JOIN checkpoint c JOIN event_log e ON e.EventStreamId=s.EventStreamId
            WHERE e.StreamVersion<=s.CurrentVersion
              AND e.StreamVersion>=coalesce(c.stream_version,1)
              AND (s.is_concern OR c.stream_version IS NULL)
            UNION ALL
            SELECT false,e.EventVersion,e.EventNameId,e.StreamVersion,e.EventPayload, e.MessageOriginJson AS MessageOriginJson
        FROM streams s CROSS JOIN checkpoint c JOIN event_log e ON e.EventStreamId=s.EventStreamId
            WHERE s.is_concern AND c.stream_version IS NOT NULL AND e.StreamVersion=1
        )
        SELECT h.concern_head,h.legacy_head,coalesce(c.stream_version,0),coalesce(e.is_legacy,false),
               coalesce(n.EventTypeName=ANY($3),false),
               e.EventVersion,n.EventTypeName,e.StreamVersion,e.EventPayload, e.MessageOriginJson AS MessageOriginJson
        FROM heads h CROSS JOIN checkpoint c LEFT JOIN selected e ON true
        LEFT JOIN event_name_id n ON n.EventNameId=e.EventNameId
        ORDER BY e.is_legacy DESC,e.StreamVersion;
        """;

    internal const string ReadAggregateCheckpointWindow = """
        WITH source AS MATERIALIZED (
            SELECT EventStreamId, LEAST(CurrentVersion, $3) AS Head
            FROM event_stream_id WHERE EventStream=$1
        ), sidecar AS MATERIALIZED (
            SELECT EventStreamId FROM event_stream_id WHERE EventStream=$2
        ), checkpoint AS MATERIALIZED (
            SELECT c.source_revision, el.*
            FROM source s
            JOIN event_aggregate_checkpoint c ON c.source_stream_id=s.EventStreamId
            JOIN event_log el ON el.EventVersion=c.checkpoint_event_version
            JOIN sidecar a ON a.EventStreamId=el.EventStreamId
            WHERE $5 < 0 AND c.source_revision<=LEAST(s.Head,$4)
            ORDER BY c.source_revision DESC LIMIT 1
        ), legacy AS MATERIALIZED (
            SELECT el.*
            FROM sidecar a JOIN event_log el ON el.EventStreamId=a.EventStreamId
            WHERE $5 < 0
              AND NOT EXISTS (SELECT FROM event_aggregate_checkpoint c WHERE c.checkpoint_event_version=el.EventVersion)
        ), selected AS (
            SELECT 1 AS Kind, source_revision, EventVersion, EventNameId, StreamVersion, EventPayload, MessageOriginJson AS MessageOriginJson
        FROM checkpoint
            UNION ALL
            SELECT 2, 0, EventVersion, EventNameId, StreamVersion, EventPayload, MessageOriginJson AS MessageOriginJson
        FROM legacy
            UNION ALL
            SELECT 3, 0, el.EventVersion, el.EventNameId, el.StreamVersion, el.EventPayload, el.MessageOriginJson AS MessageOriginJson
        FROM source s JOIN event_log el ON el.EventStreamId=s.EventStreamId
            WHERE (EXISTS (SELECT FROM checkpoint) OR NOT EXISTS (SELECT FROM legacy))
              AND el.StreamVersion>COALESCE((SELECT source_revision FROM checkpoint),GREATEST($5,0))
              AND el.StreamVersion<=s.Head
        )
        SELECT s.Head, chosen.Kind, chosen.source_revision, chosen.EventVersion,
               n.EventTypeName, chosen.StreamVersion, chosen.EventPayload, chosen.MessageOriginJson AS MessageOriginJson
        FROM source s LEFT JOIN selected chosen ON true
        LEFT JOIN event_name_id n ON n.EventNameId=chosen.EventNameId
        ORDER BY chosen.Kind, chosen.StreamVersion;
        """;

    internal const string ReadAggregateEventByCommand = """
        SELECT el.EventVersion, n.EventTypeName, el.StreamVersion, el.EventPayload, el.MessageOriginJson AS MessageOriginJson
        FROM event_log el JOIN event_stream_id s USING(EventStreamId) JOIN event_name_id n USING(EventNameId)
        WHERE el.CommandId=$2 AND s.EventStream=$1
        LIMIT 2;
        """;

    internal const string ReadAggregateCreation = """
        SELECT el.EventVersion, n.EventTypeName, el.StreamVersion, el.EventPayload, el.MessageOriginJson AS MessageOriginJson
        FROM event_stream_id s JOIN event_log el USING(EventStreamId) JOIN event_name_id n USING(EventNameId)
        WHERE s.EventStream=$1 AND el.StreamVersion=1;
        """;

    internal const string LockAggregateCheckpointSource = """
        SELECT CurrentVersion FROM event_stream_id WHERE EventStream=$1 FOR UPDATE;
        """;

    internal const string ReadAggregateCheckpointHead = """
        SELECT CurrentVersion FROM event_stream_id WHERE EventStream=$1;
        """;

    internal const string ReadAggregateCheckpointVersion = """
        SELECT c.checkpoint_event_version
        FROM event_stream_id s JOIN event_aggregate_checkpoint c ON c.source_stream_id=s.EventStreamId
        WHERE s.EventStream=$1 AND c.source_revision=$2;
        """;

    internal const string RegisterAggregateCheckpoint = """
        INSERT INTO event_aggregate_checkpoint(source_stream_id,source_revision,checkpoint_event_version)
        SELECT EventStreamId,$2,$3 FROM event_stream_id WHERE EventStream=$1;
        """;
    /// <summary>Reads creation, the latest replacement for each declared component, and the captured head without scanning superseded payloads.</summary>
    public const string GetEventLogFromLatestComponentSnapshots = """
        WITH snapshot_head AS MATERIALIZED (
            SELECT CurrentVersion FROM event_stream_id WHERE EventStreamId=$1
        ), selected AS MATERIALIZED (
            SELECT 1::bigint AS StreamVersion
            UNION SELECT CurrentVersion FROM snapshot_head
            UNION
            SELECT latest.StreamVersion
            FROM unnest($2::integer[]) AS names(EventNameId)
            CROSS JOIN snapshot_head
            CROSS JOIN LATERAL (
                SELECT el.StreamVersion FROM event_log el
                WHERE el.EventStreamId=$1 AND el.EventNameId=names.EventNameId
                    AND el.StreamVersion<=snapshot_head.CurrentVersion
                ORDER BY el.EventVersion DESC LIMIT 1
            ) latest
        )
        SELECT el.EventStreamId,en.EventName,en.EventTypeName,el.EventVersion,
               el.EventPayload,el.CommandId,el.EventTimestamp,el.StreamVersion,
               snapshot_head.CurrentVersion AS SnapshotHead,el.StreamVersion AS CheckpointVersion, el.MessageOriginJson AS MessageOriginJson
        FROM snapshot_head CROSS JOIN selected
        LEFT JOIN event_log el ON el.EventStreamId=$1 AND el.StreamVersion=selected.StreamVersion
        LEFT JOIN event_name_id en ON en.EventNameId=el.EventNameId
        WHERE selected.StreamVersion>0
        ORDER BY selected.StreamVersion;
        """;

    /// <summary>Restores a stateless reducer without reading its raw tick history.</summary>
    public const string GetStatelessEventLogFromCreationSnapshot = """
        WITH snapshot_head AS MATERIALIZED (
            SELECT CurrentVersion FROM event_stream_id WHERE EventStreamId=$1
        )
        SELECT el.EventStreamId,en.EventName,en.EventTypeName,el.EventVersion,
               el.EventPayload,el.CommandId,el.EventTimestamp,el.StreamVersion,
               snapshot_head.CurrentVersion AS SnapshotHead,snapshot_head.CurrentVersion AS CheckpointVersion, el.MessageOriginJson AS MessageOriginJson
        FROM snapshot_head
        LEFT JOIN LATERAL (
            SELECT * FROM event_log creation
            WHERE creation.EventStreamId=$1 AND creation.StreamVersion=1
              AND creation.StreamVersion<=snapshot_head.CurrentVersion
            UNION ALL
            SELECT * FROM event_log head
            WHERE head.EventStreamId=$1 AND head.StreamVersion=snapshot_head.CurrentVersion
              AND head.StreamVersion>1
        ) el ON true
        LEFT JOIN event_name_id en ON en.EventNameId=el.EventNameId
        ORDER BY el.StreamVersion;
        """;

    /// <summary>Reads the first committed source fact and its ordered tail at one MVCC snapshot.</summary>
    public const string GetEventLogFromCreationSnapshot = """
        WITH snapshot_head AS MATERIALIZED (
            SELECT CurrentVersion FROM event_stream_id WHERE EventStreamId = $1
        )
        SELECT el.EventStreamId, en.EventName, en.EventTypeName, el.EventVersion,
               el.EventPayload, el.CommandId, el.EventTimestamp, el.StreamVersion,
               snapshot_head.CurrentVersion AS SnapshotHead, el.MessageOriginJson AS MessageOriginJson
        FROM snapshot_head
        LEFT JOIN event_log el ON el.EventStreamId = $1
            AND el.StreamVersion >= 1 AND el.StreamVersion <= snapshot_head.CurrentVersion
        LEFT JOIN event_name_id en ON en.EventNameId = el.EventNameId
        ORDER BY el.StreamVersion;
        """;

    /// <summary>Retains creation while skipping only facts superseded by an explicit full checkpoint.</summary>
    public const string GetEventLogFromCreationWithCheckpoint =
        "WITH names AS (SELECT unnest($2::integer[]) AS EventNameId), " + CreationWithCheckpointQuery;

    /// <summary>Resolves explicitly known checkpoint identities inside the same statement as the stream read.</summary>
    public const string GetEventLogFromCreationWithCheckpointIdentities =
        "WITH names AS (SELECT EventNameId FROM event_name_id WHERE EventTypeName = ANY($2)), " + CreationWithCheckpointQuery;

    private const string CreationWithCheckpointQuery = """
        snapshot_head AS MATERIALIZED (
            SELECT CurrentVersion FROM event_stream_id WHERE EventStreamId = $1
        ), checkpoint AS MATERIALIZED (
            SELECT coalesce(max(latest.StreamVersion),1) AS StreamVersion
            FROM names
            CROSS JOIN snapshot_head
            CROSS JOIN LATERAL (
                SELECT el.StreamVersion FROM event_log el
                WHERE el.EventStreamId=$1 AND el.EventNameId=names.EventNameId
                  AND el.StreamVersion<=snapshot_head.CurrentVersion
                ORDER BY el.EventVersion DESC LIMIT 1
            ) latest
        )
        SELECT el.EventStreamId, en.EventName, en.EventTypeName, el.EventVersion,
               el.EventPayload, el.CommandId, el.EventTimestamp, el.StreamVersion,
               snapshot_head.CurrentVersion AS SnapshotHead, checkpoint.StreamVersion AS CheckpointVersion, el.MessageOriginJson AS MessageOriginJson
        FROM snapshot_head CROSS JOIN checkpoint
        LEFT JOIN LATERAL (
            SELECT * FROM event_log creation
            WHERE creation.EventStreamId=$1 AND creation.StreamVersion=1
              AND creation.StreamVersion<=snapshot_head.CurrentVersion
            UNION ALL
            SELECT * FROM event_log tail
            WHERE tail.EventStreamId=$1 AND tail.StreamVersion>1
              AND tail.StreamVersion>=checkpoint.StreamVersion
              AND tail.StreamVersion<=snapshot_head.CurrentVersion
        ) el ON true
        LEFT JOIN event_name_id en ON en.EventNameId=el.EventNameId
        ORDER BY el.StreamVersion;
        """;

    /// <summary>Finds current, stable and historical metadata rows for a typed read without rewriting them.</summary>
    public const string GetCompatibleEventNameIds = """
        SELECT EventNameId, EventName, EventTypeName FROM event_name_id
        WHERE EventName = $1 OR EventTypeName = ANY($2);
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
      cl.CommandData as "CommandData",
      cl.CommandPayload as "CommandPayload",
      cl.CommandPayloadFormat as "CommandPayloadFormat",
      cl.CommandPayloadVersion as "CommandPayloadVersion",
      cl.CommandPayloadSha256 as "CommandPayloadSha256",
      cl.MessageOriginJson AS MessageOriginJson
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
        el.EventPayload as "EventPayload",
        el.commandId as "CommandId",
        el.eventTimestamp as "EventTimeStamp",
        el.StreamVersion as "StreamVersion", el.MessageOriginJson AS MessageOriginJson
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
      el.EventPayload as "EventPayload",
      el.commandId as "CommandId",
      el.eventTimestamp as "EventTimeStamp",
      el.StreamVersion as "StreamVersion", el.MessageOriginJson AS MessageOriginJson
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
      el.EventPayload as "EventPayload",
      el.commandId as "CommandId",
      el.eventTimestamp as "EventTimeStamp",
      el.StreamVersion as "StreamVersion", el.MessageOriginJson AS MessageOriginJson
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
        el.EventPayload as "EventPayload",
        el.commandId as "CommandId",
        el.eventTimestamp as "EventTimeStamp",
        el.StreamVersion as "StreamVersion", el.MessageOriginJson AS MessageOriginJson
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
        el.StreamVersion,
        el.EventPayload,
        el.commandId,
        el.eventTimestamp, el.MessageOriginJson AS MessageOriginJson
        FROM event_log el
    WHERE el.eventStreamId = $1
      AND el.eventNameId = ANY($2)
    ORDER BY el.eventVersion DESC
    LIMIT GREATEST($3, 0)
)
SELECT
    el.eventStreamId AS "EventStreamId",
    en.eventName AS "EventName",
    en.eventTypeName AS "EventTypeName",
    el.eventVersion AS "EventVersion",
    el.EventPayload AS "EventPayload",
    el.commandId AS "CommandId",
    el.eventTimestamp AS "EventTimeStamp",
    el.StreamVersion AS "StreamVersion", el.MessageOriginJson AS MessageOriginJson
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
WITH snapshot_versions AS MATERIALIZED (
    SELECT el.eventVersion
    FROM event_log el
    WHERE el.eventStreamId = $1
      AND el.eventNameId = ANY($2)
),
latest_snapshot AS (
    SELECT MAX(eventVersion) AS snapshotVersion FROM snapshot_versions
),
last_event_range AS (
    SELECT
        el.eventStreamId,
        el.eventNameId,
        el.eventVersion,
        el.StreamVersion,
        el.EventPayload,
        el.commandId,
        el.eventTimestamp, el.MessageOriginJson AS MessageOriginJson
        FROM event_log el
    CROSS JOIN latest_snapshot snapshot
    WHERE snapshot.snapshotVersion IS NOT NULL
      AND el.eventStreamId = $1
      AND el.eventNameId = ANY($3)
      AND el.eventVersion > snapshot.snapshotVersion
    ORDER BY el.eventVersion DESC
    LIMIT GREATEST($4, 0)
),
replay_range AS (
    SELECT
        el.eventStreamId,
        el.eventNameId,
        el.eventVersion,
        el.StreamVersion,
        el.EventPayload,
        el.commandId,
        el.eventTimestamp, el.MessageOriginJson AS MessageOriginJson
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
        StreamVersion,
        EventPayload,
        commandId,
        eventTimestamp, MessageOriginJson AS MessageOriginJson
        FROM last_event_range
)
SELECT
    el.eventStreamId AS "EventStreamId",
    en.eventName AS "EventName",
    en.eventTypeName AS "EventTypeName",
    el.eventVersion AS "EventVersion",
    el.EventPayload AS "EventPayload",
    el.commandId AS "CommandId",
    el.eventTimestamp AS "EventTimeStamp",
    el.StreamVersion AS "StreamVersion", el.MessageOriginJson AS MessageOriginJson
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
            BlockedStage as "BlockedStage",
            StreamVersion as "StreamVersion"
        """;

    public const string TryCreateEventProjectorExecutionState = """
        INSERT INTO event_projector_state (
            EventId, ActorName, ProjectorName, IsReplay, AttemptNumber,
            Outcome, Stage, ErrorMessage, CreatedTimestamp, UpdatedTimestamp,
            EventStreamId, SourceEventName, UpdatedAtUtc, StreamVersion
        )
        SELECT
            $1, $2, $3, $4, $5,
            CASE
                WHEN checkpoint.LastAppliedStreamVersion IS NULL
                  OR checkpoint.LastAppliedStreamVersion < el.StreamVersion THEN $6
                WHEN checkpoint.LastAppliedStreamVersion = el.StreamVersion THEN 'AlreadyCompleted'
                ELSE 'Superseded'
            END,
            CASE WHEN checkpoint.LastAppliedStreamVersion >= el.StreamVersion THEN 'Completed' ELSE $7 END,
            CASE
                WHEN checkpoint.LastAppliedStreamVersion >= el.StreamVersion
                    THEN 'stream-checkpoint-covered'
                ELSE $8
            END,
            $9, $10,
            el.EventStreamId, en.EventName, $11, el.StreamVersion
        FROM event_log el
        JOIN event_name_id en ON en.EventNameId = el.EventNameId
        LEFT JOIN event_projector_stream_checkpoint checkpoint
          ON checkpoint.ProjectorName = $3
         AND checkpoint.EventStreamId = el.EventStreamId
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
        WITH reconciled AS (
            UPDATE event_projector_state current_state
            SET Stage = 'Completed',
                Outcome = CASE
                    WHEN checkpoint.LastAppliedStreamVersion = current_state.StreamVersion
                        THEN 'AlreadyCompleted'
                    ELSE 'Superseded'
                END,
                ErrorMessage = 'stream-checkpoint-covered',
                BlockedReason = 'stream-checkpoint-covered',
                ExecutionToken = NULL,
                LeaseExpiresAtUtc = NULL,
                Revision = current_state.Revision + 1 /* Revision = Revision + 1 */,
                UpdatedAtUtc = $5,
                UpdatedTimestamp = $6
            FROM event_projector_stream_checkpoint checkpoint
            WHERE current_state.EventId = $1
              AND current_state.ProjectorName = $2
              AND checkpoint.ProjectorName = current_state.ProjectorName
              AND checkpoint.EventStreamId = current_state.EventStreamId
              AND checkpoint.LastAppliedStreamVersion >= current_state.StreamVersion
              AND current_state.Outcome IN ('Processing', 'Retrying')
              AND current_state.Stage IN ('ValidateSourceEvent', 'PublishProcessingEvent', 'ApplyProjection')
            RETURNING current_state.*
        ), claimed AS (
            UPDATE event_projector_state current_state
            SET ExecutionToken = $3,
                LeaseExpiresAtUtc = $4,
                Revision = current_state.Revision + 1 /* Revision = Revision + 1 */,
                UpdatedAtUtc = $5,
                UpdatedTimestamp = $6
            WHERE current_state.EventId = $1
              AND current_state.ProjectorName = $2
              AND current_state.Outcome IN ('Processing', 'Retrying')
              AND NOT EXISTS (SELECT 1 FROM reconciled)
              AND (current_state.ExecutionToken IS NULL OR current_state.LeaseExpiresAtUtc IS NULL OR current_state.LeaseExpiresAtUtc <= $5)
              AND NOT EXISTS (
                  SELECT 1
                  FROM event_projector_state earlier
                  WHERE earlier.ProjectorName = current_state.ProjectorName
                    AND earlier.EventStreamId = current_state.EventStreamId
                    AND earlier.StreamVersion < current_state.StreamVersion
                    AND earlier.Outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded')
              )
            RETURNING current_state.*
        )
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM reconciled
        UNION ALL
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM claimed
        LIMIT 1;
        """;

    public const string HasEarlierUnresolvedEventProjectorExecution = """
        SELECT EXISTS (
            SELECT 1
            FROM event_projector_state current_state
            JOIN event_projector_state earlier
              ON earlier.ProjectorName = current_state.ProjectorName
             AND earlier.EventStreamId = current_state.EventStreamId
             AND earlier.StreamVersion < current_state.StreamVersion
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
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                ProjectorName, EventStreamId, LastAppliedStreamVersion,
                LastAppliedEventId, Revision, UpdatedAtUtc)
            SELECT ProjectorName, EventStreamId, StreamVersion, EventId, 0, $15
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 <> 'PublishFailedEvent'
            ON CONFLICT (ProjectorName, EventStreamId) DO UPDATE SET
                LastAppliedStreamVersion = EXCLUDED.LastAppliedStreamVersion,
                LastAppliedEventId = EXCLUDED.LastAppliedEventId,
                Revision = event_projector_stream_checkpoint.Revision + 1,
                UpdatedAtUtc = EXCLUDED.UpdatedAtUtc
            WHERE event_projector_stream_checkpoint.LastAppliedStreamVersion < EXCLUDED.LastAppliedStreamVersion
        )
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM transitioned;
        """;

    public const string TryTerminalizeEventProjectorExecution = """
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
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                ProjectorName, EventStreamId, LastAppliedStreamVersion,
                LastAppliedEventId, Revision, UpdatedAtUtc)
            SELECT ProjectorName, EventStreamId, StreamVersion, EventId, 0, $13
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 = 'Completed'
            ON CONFLICT (ProjectorName, EventStreamId) DO UPDATE SET
                LastAppliedStreamVersion = EXCLUDED.LastAppliedStreamVersion,
                LastAppliedEventId = EXCLUDED.LastAppliedEventId,
                Revision = event_projector_stream_checkpoint.Revision + 1,
                UpdatedAtUtc = EXCLUDED.UpdatedAtUtc
            WHERE event_projector_stream_checkpoint.LastAppliedStreamVersion < EXCLUDED.LastAppliedStreamVersion
        )
        SELECT
        """ + EventProjectorExecutionStateColumns + """
        FROM transitioned;
        """;

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
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                ProjectorName, EventStreamId, LastAppliedStreamVersion,
                LastAppliedEventId, Revision, UpdatedAtUtc)
            SELECT ProjectorName, EventStreamId, StreamVersion, EventId, 0, $15
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 <> 'PublishFailedEvent'
            ON CONFLICT (ProjectorName, EventStreamId) DO UPDATE SET
                LastAppliedStreamVersion = EXCLUDED.LastAppliedStreamVersion,
                LastAppliedEventId = EXCLUDED.LastAppliedEventId,
                Revision = event_projector_stream_checkpoint.Revision + 1,
                UpdatedAtUtc = EXCLUDED.UpdatedAtUtc
            WHERE event_projector_stream_checkpoint.LastAppliedStreamVersion < EXCLUDED.LastAppliedStreamVersion
        ), staged AS (
            INSERT INTO event_projector_outbox (
                ProjectorName, EventId, EffectKind, MessageId, EventTypeName,
                EventPayload, Status, AttemptCount, NextAttemptAtUtc, CreatedAtUtc, LastError, MessageOriginJson)
            SELECT ProjectorName, EventId, $17, $18, $19,
                   $20, 'Pending', 0, $21, $21, '', $22
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
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                ProjectorName, EventStreamId, LastAppliedStreamVersion,
                LastAppliedEventId, Revision, UpdatedAtUtc)
            SELECT ProjectorName, EventStreamId, StreamVersion, EventId, 0, $13
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 = 'Completed'
            ON CONFLICT (ProjectorName, EventStreamId) DO UPDATE SET
                LastAppliedStreamVersion = EXCLUDED.LastAppliedStreamVersion,
                LastAppliedEventId = EXCLUDED.LastAppliedEventId,
                Revision = event_projector_stream_checkpoint.Revision + 1,
                UpdatedAtUtc = EXCLUDED.UpdatedAtUtc
            WHERE event_projector_stream_checkpoint.LastAppliedStreamVersion < EXCLUDED.LastAppliedStreamVersion
        ), staged AS (
            INSERT INTO event_projector_outbox (
                ProjectorName, EventId, EffectKind, MessageId, EventTypeName,
                EventPayload, Status, AttemptCount, NextAttemptAtUtc, CreatedAtUtc, LastError, MessageOriginJson)
            SELECT ProjectorName, EventId, $15, $16, $17,
                   $18, 'Pending', 0, $19, $19, '', $20
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
            o.DispatchLeaseExpiresAtUtc as "DispatchLeaseExpiresAtUtc",
            o.MessageOriginJson AS MessageOriginJson;
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
            el.EventPayload as "EventPayload",
            el.CommandId as "CommandId",
            el.EventTimestamp as "EventTimestamp",
            el.StreamVersion as "StreamVersion",
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
            eps.UpdatedAtUtc as "UpdatedAtUtc",
            eps.BlockedStage as "BlockedStage",
            eps.StreamVersion as "StateStreamVersion", el.MessageOriginJson AS MessageOriginJson
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

    public const string InsertCommandLogMessagePackWindow = """
        WITH input_raw AS (
            SELECT *
            FROM unnest(
                $1::uuid[], $2::text[], $3::text[], $4::text[], $5::text[], $6::text[], $7::text[],
                $8::bytea[], $9::smallint[], $10::integer[], $11::bytea[], $12::text[])
            WITH ORDINALITY AS i(
                CommandId, StreamId, ActorName, CommandName, CommandTimestamp, CommandStatus, CommandData,
                CommandPayload, CommandPayloadFormat, CommandPayloadVersion, CommandPayloadSha256, MessageOriginJson, Ordinal)
        ),
        input AS (
            SELECT *, row_number() OVER (PARTITION BY CommandId ORDER BY Ordinal) AS CommandOccurrence
            FROM input_raw
        ),
        inserted AS (
            INSERT INTO command_log (
                CommandId, StreamId, ActorName, CommandName, CommandTimestamp, CommandStatus, CommandData,
                CommandPayload, CommandPayloadFormat, CommandPayloadVersion, CommandPayloadSha256, MessageOriginJson)
            SELECT CommandId, StreamId, ActorName, CommandName, CommandTimestamp, CommandStatus, CommandData,
                   CommandPayload, CommandPayloadFormat, CommandPayloadVersion, CommandPayloadSha256, MessageOriginJson
            FROM input
            WHERE CommandOccurrence = 1
            ORDER BY Ordinal
            ON CONFLICT (CommandId) DO NOTHING
            RETURNING CommandId, CommandPayloadSha256
        )
        SELECT i.Ordinal, i.CommandId,
               (n.CommandId IS NOT NULL AND i.CommandOccurrence = 1) AS Accepted,
               COALESCE(n.CommandPayloadSha256, c.CommandPayloadSha256) AS StoredPayloadSha256
        FROM input i
        LEFT JOIN inserted n ON n.CommandId = i.CommandId
        LEFT JOIN command_log c ON c.CommandId = i.CommandId
        ORDER BY i.Ordinal;
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
            el.EventPayload as "EventPayload",
            el.commandId as "CommandId",
            el.eventTimestamp as "EventTimestamp",
            el.StreamVersion as "StreamVersion", el.MessageOriginJson AS MessageOriginJson
        FROM event_log el
        JOIN event_name_id en ON el.eventNameId = en.eventNameId
        JOIN event_projector_state eps
          ON eps.EventId = el.eventVersion
         AND eps.ProjectorName = $1
        WHERE en.eventName = ANY(string_to_array($2, ','))
          AND eps.Outcome IN ('Processing', 'Retrying')
        ORDER BY el.EventStreamId, el.StreamVersion;
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
    /// Finds the newest compatible snapshot with one indexed seek per event-name
    /// alias. Limits apply before the aggregate so dense streams and missing
    /// snapshots cannot scan either full stream history or the global version index.
    /// </summary>
    public const string GetMaxEventVersion = """
WITH snapshot_versions AS MATERIALIZED (
    SELECT latest.eventVersion
    FROM unnest($2::integer[]) AS names(eventNameId)
    CROSS JOIN LATERAL (
        SELECT el.eventVersion
        FROM event_log el
        WHERE el.eventStreamId = $1
          AND el.eventNameId = names.eventNameId
        ORDER BY el.eventVersion DESC
        LIMIT 1
    ) latest
)
SELECT max(eventVersion) as "MaxEventVersion"
FROM snapshot_versions
""";

    /// <summary>
    /// SQL to insert an event log
    /// </summary>
    public const string InsertEventLog = """
    WITH next_stream_version AS (
        UPDATE event_stream_id
        SET CurrentVersion = CurrentVersion + 1
        WHERE EventStreamId = $1
        RETURNING CurrentVersion AS StreamVersion
    )
    INSERT INTO event_log (
            EventStreamId,
            EventNameId,
            StreamVersion,
            EventPayload,
            CommandId,
            EventTimestamp,
            MessageOriginJson
        )
        SELECT
            $1,
            $2,
            next_stream_version.StreamVersion,
            $3,
            $4,
            $5,
            $6
        FROM next_stream_version
        RETURNING EventVersion, StreamVersion;
    """;

    /// <summary>Inserts an event only when the stream is still at the caller's observed version.</summary>
    public const string InsertEventLogExpectedVersion = """
    WITH next_stream_version AS (
        UPDATE event_stream_id
        SET CurrentVersion = CurrentVersion + 1
        WHERE EventStreamId = $1
          AND CurrentVersion = $6
        RETURNING CurrentVersion AS StreamVersion
    )
    INSERT INTO event_log (
            EventStreamId,
            EventNameId,
            StreamVersion,
            EventPayload,
            CommandId,
            EventTimestamp,
            MessageOriginJson
        )
        SELECT
            $1,
            $2,
            next_stream_version.StreamVersion,
            $3,
            $4,
            $5,
            $7
        FROM next_stream_version
        RETURNING EventVersion;
    """;

    public const string GetEventProjectorStreamCheckpoint = """
        SELECT ProjectorName as "ProjectorName",
               EventStreamId as "EventStreamId",
               LastAppliedStreamVersion as "LastAppliedStreamVersion",
               LastAppliedEventId as "LastAppliedEventId",
               Revision as "Revision",
               UpdatedAtUtc as "UpdatedAtUtc"
        FROM event_projector_stream_checkpoint
        WHERE ProjectorName = $1 AND EventStreamId = $2;
        """;

    public const string UpdateEventLog = """
    UPDATE event_log SET
        EventPayload = $1,
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
            UpdatedAtUtc,
            StreamVersion
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
            CURRENT_TIMESTAMP,
            el.StreamVersion
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
            StreamVersion = COALESCE(event_projector_state.StreamVersion, EXCLUDED.StreamVersion),
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
)
ON CONFLICT (EventName, EventTypeName) DO UPDATE
SET EventName = EXCLUDED.EventName
RETURNING eventNameId;
""";

    /// <summary>
    /// SQL to insert an event stream ID
    /// </summary>
    public const string InsertEventStreamId = """
INSERT INTO event_stream_id (
    EventStream
) VALUES (
    $1
)
ON CONFLICT (EventStream) DO UPDATE
SET EventStream = EXCLUDED.EventStream
RETURNING eventStreamId;
""";
    internal const string HasCommittedStreamWithPrefix = """
        SELECT EXISTS (
            SELECT 1 FROM public.event_stream_id s
            WHERE s.currentversion > 0 AND NOT (s.eventstream = ANY($1::text[]))
              AND EXISTS (SELECT 1 FROM unnest($2::text[]) p(prefix) WHERE starts_with(s.eventstream,p.prefix)));
        """;
    internal const string HasCommittedMaintenanceStream = "SELECT currentversion FROM public.event_stream_id WHERE eventstream=$1 AND currentversion>0;";
    internal const string ReadMaintenanceSourceEvents = """
        SELECT l.eventstreamid,n.eventname,n.eventtypename,l.eventversion,l.eventpayload,
               l.commandid,l.eventtimestamp,l.streamversion,s.eventstream
        FROM public.event_stream_id s
        CROSS JOIN LATERAL (SELECT * FROM public.event_log l WHERE l.eventstreamid=s.eventstreamid OFFSET 0) l
        JOIN public.event_name_id n ON n.eventnameid=l.eventnameid
        WHERE s.eventstream=ANY($1::text[])
           OR EXISTS (SELECT 1 FROM unnest($2::text[]) p(prefix) WHERE starts_with(s.eventstream,p.prefix))
        ORDER BY l.eventversion;
        """;
    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string EnlistedEventTransaction_ReserveCommandAsync_1 = "SELECT commandid FROM command_log WHERE commandid=$1 FOR UPDATE;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string EnlistedEventTransaction_ReserveCommandAsync_2 = "SELECT EXISTS(SELECT 1 FROM event_log WHERE commandid=$1);";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string EnlistedEventTransaction_ReserveCommandAsync_3 = "SELECT EXISTS(SELECT 1 FROM event_log l JOIN event_stream_id s ON l.eventstreamid=s.eventstreamid WHERE l.commandid=$1 AND s.eventstream=$2);";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string EnlistedEventTransaction_AppendAsync_1 = "SELECT eventstreamid FROM event_stream_id WHERE eventstream=$1;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string EnlistedEventTransaction_AppendAsync_2 = "SELECT eventnameid FROM event_name_id WHERE eventname=$1 AND eventtypename=$2;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string EventSourceActorDbContext_GetCommittedStreamVersionAsync_1 = "SELECT COALESCE(MAX(StreamVersion),0) FROM event_log WHERE EventStreamId=$1;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresCommittedBusinessEventJournal_Statement_1 = "SELECT el.eventStreamId,en.eventName,en.eventTypeName,el.eventVersion,el.EventPayload,el.commandId,el.eventTimestamp::text,el.StreamVersion FROM event_log el JOIN event_name_id en ON en.eventNameId=el.eventNameId ";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresCommittedBusinessEventJournal_CompleteHandoffAsync_1 = "UPDATE business_subscription_projection_receipt SET handoff_completed=true WHERE event_id=$1;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresCommittedBusinessEventJournal_RejectAsync_1 = "INSERT INTO business_subscription_projection_issue(event_id,reason_code,detail) VALUES($1,$2,$3) ON CONFLICT DO NOTHING;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresCommittedBusinessEventJournal_RejectAsync_2 = "INSERT INTO business_subscription_projection_receipt(event_id) VALUES($1) ON CONFLICT DO NOTHING;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresQueryTimeoutScope_ApplyAsync_1 = "SELECT set_config('statement_timeout',$1,true), set_config('lock_timeout',$2,true);";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_LoadCursorAsync_1 = "SELECT after_event_version FROM risk_history_projection_progress WHERE projection_name=$1;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_LoadCursorAsync_2 = """
                INSERT INTO risk_history_projection_receipt(projection_name,event_id)
                SELECT $1,e.eventversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE n.eventname='WorkflowStrategyStateUpdatedEvent' AND e.eventversion<=$2
                ON CONFLICT DO NOTHING;
                """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_SaveCursorAsync_1 = """
                INSERT INTO risk_history_projection_progress(projection_name,after_event_version) VALUES($2,$1)
                ON CONFLICT(projection_name) DO UPDATE
                SET after_event_version=GREATEST(risk_history_projection_progress.after_event_version,EXCLUDED.after_event_version);
                """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_PageAsync_1 = """
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE n.eventname='WorkflowStrategyStateUpdatedEvent'
                  AND NOT EXISTS(
                    SELECT 1 FROM risk_history_projection_receipt r
                    WHERE r.projection_name=$1 AND r.event_id=e.eventversion)
                ORDER BY e.eventversion LIMIT 32;
                """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_AcknowledgeAsync_1 = "INSERT INTO risk_history_projection_receipt(projection_name,event_id) VALUES($1,$2) ON CONFLICT DO NOTHING;";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_QuarantineConflictAsync_1 = """
                INSERT INTO risk_history_projection_issue(
                  projection_name,event_id,workflow_id,invocation_id,source_revision,issue_code,stored_hash,incoming_hash)
                VALUES($1,$2,$3,$4,$5,'ImmutableRevisionConflict',$6,$7)
                ON CONFLICT DO NOTHING;
                """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string RiskHistoryJournal_ProjectCompletionPageAsync_1 = """
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE n.eventname=$1
                  AND NOT EXISTS(
                    SELECT 1 FROM risk_history_projection_receipt r
                    WHERE r.projection_name=$2 AND r.event_id=e.eventversion)
                ORDER BY e.eventversion LIMIT 32;
                """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string CommandAuditPostgres_MatchesStoredRetryAsync_1 = "SELECT commandpayload,commandpayloadformat,commandpayloadversion,commandpayloadsha256 FROM command_log WHERE commandid=$1";

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string BatchedProjectionMarkerWriter_Statement_1 = """
        WITH requested AS (
            SELECT * FROM unnest($1::bigint[], $2::text[], $3::text[], $4::text[])
                AS input(EventId, ActorName, ProjectorName, InitialStage)
        ), inserted AS (
            INSERT INTO event_projector_state (
                EventId, ActorName, ProjectorName, IsReplay, AttemptNumber,
                Outcome, Stage, ErrorMessage, CreatedTimestamp, UpdatedTimestamp,
                EventStreamId, SourceEventName, UpdatedAtUtc, StreamVersion
            )
            SELECT input.EventId, input.ActorName, input.ProjectorName, false, 0,
                CASE
                    WHEN checkpoint.LastAppliedStreamVersion IS NULL
                      OR checkpoint.LastAppliedStreamVersion < el.StreamVersion THEN 'Processing'
                    WHEN checkpoint.LastAppliedStreamVersion = el.StreamVersion THEN 'AlreadyCompleted'
                    ELSE 'Superseded'
                END,
                CASE WHEN checkpoint.LastAppliedStreamVersion >= el.StreamVersion
                    THEN 'Completed' ELSE input.InitialStage END,
                CASE WHEN checkpoint.LastAppliedStreamVersion >= el.StreamVersion
                    THEN 'stream-checkpoint-covered' ELSE '' END,
                $5, $5, el.EventStreamId, en.EventName, $6, el.StreamVersion
            FROM requested input
            JOIN event_log el ON el.EventVersion = input.EventId
            JOIN event_name_id en ON en.EventNameId = el.EventNameId
            LEFT JOIN event_projector_stream_checkpoint checkpoint
                ON checkpoint.ProjectorName = input.ProjectorName
               AND checkpoint.EventStreamId = el.EventStreamId
            ON CONFLICT (EventId, ProjectorName) DO NOTHING
            RETURNING EventId
        )
        SELECT count(*) FROM inserted;
        """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string BinaryCopyEventLogAppender_LockStreamsAsync_1 = """
            SELECT EventStreamId, CurrentVersion
            FROM event_stream_id
            WHERE EventStreamId = ANY($1)
            ORDER BY EventStreamId
            FOR UPDATE;
            """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string BinaryCopyEventLogAppender_ReserveEventIdsAsync_1 = "SELECT nextval('public.event_log_eventversion_seq') FROM generate_series(1, $1);";

    internal const string ReserveCopiedStreamVersions = """
            UPDATE event_stream_id AS stream
            SET CurrentVersion = updates.FinalVersion
            FROM unnest($1::bigint[], $2::bigint[]) AS updates(EventStreamId, FinalVersion)
            WHERE stream.EventStreamId = updates.EventStreamId;
            """;
}
