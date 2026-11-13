namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>SQL statements and query templates for EventSourceActor storage.</summary>
public static class EventSourceActorDbSql
{
    /// <summary>Reads unnest rows.</summary>
    internal const string BatchedProjectionMarkerWriter_Statement_1 =
        """
        WITH requested AS (
            SELECT * FROM unnest($1::bigint[], $2::text[], $3::text[], $4::text[])
                AS input(eventid, actorname, projectorname, initialstage)
        ), inserted AS (
            INSERT INTO event_projector_state (
                eventid, actorname, projectorname, isreplay, attemptnumber,
                outcome, stage, errormessage, createdtimestamp, updatedtimestamp,
                eventstreamid, sourceeventname, updatedatutc, streamversion
            )
            SELECT input.eventid, input.actorname, input.projectorname, FALSE, 0,
                CASE
                    WHEN checkpoint.lastappliedstreamversion IS NULL
                      OR checkpoint.lastappliedstreamversion < el.streamversion THEN 'Processing'
                    WHEN checkpoint.lastappliedstreamversion = el.streamversion THEN 'AlreadyCompleted'
                    ELSE 'Superseded'
                END,
                CASE WHEN checkpoint.lastappliedstreamversion >= el.streamversion
                    THEN 'Completed' ELSE input.initialstage END,
                CASE WHEN checkpoint.lastappliedstreamversion >= el.streamversion
                    THEN 'stream-checkpoint-covered' ELSE '' END,
                $5, $5, el.eventstreamid, en.eventname, $6, el.streamversion
            FROM requested input
            JOIN event_log el ON el.eventversion = input.eventid
            JOIN event_name_id en ON en.eventnameid = el.eventnameid
            LEFT JOIN event_projector_stream_checkpoint checkpoint
                ON checkpoint.projectorname = input.projectorname
               AND checkpoint.eventstreamid = el.eventstreamid
            ON CONFLICT (eventid, projectorname) DO NOTHING
            RETURNING eventid
        )
        SELECT COUNT(*) FROM inserted;
        """;

    /// <summary>Reads and locks event stream id rows.</summary>
    internal const string BinaryCopyEventLogAppender_LockStreamsAsync_1 =
        """
        SELECT eventstreamid, currentversion
        FROM event_stream_id
        WHERE eventstreamid = ANY($1)
        ORDER BY eventstreamid
        FOR UPDATE;
        """;

    /// <summary>Reads generate series rows.</summary>
    internal const string BinaryCopyEventLogAppender_ReserveEventIdsAsync_1 =
        """
        SELECT nextval('public.event_log_eventversion_seq') FROM generate_series(1, $1);
        """;

    /// <summary>SQL for claim event projector outbox.</summary>
    internal const string ClaimEventProjectorOutbox =
        """
        WITH candidates AS (
            SELECT projectorname, eventid, effectkind
            FROM event_projector_outbox
            WHERE projectorname = $1
              AND (
                    (status IN ('Pending', 'Retrying') AND (nextattemptatutc IS NULL OR nextattemptatutc <= $4))
                 OR (status = 'Publishing' AND dispatchleaseexpiresatutc <= $4)
              )
            ORDER BY createdatutc, eventid, effectkind
            LIMIT $5
            FOR UPDATE SKIP LOCKED
        )
        UPDATE event_projector_outbox o
        SET status = 'Publishing',
            attemptcount = o.attemptcount + 1,
            dispatchtoken = $2,
            dispatchleaseexpiresatutc = $3,
            nextattemptatutc = NULL
        FROM candidates c
        WHERE o.projectorname = c.projectorname
          AND o.eventid = c.eventid
          AND o.effectkind = c.effectkind
        RETURNING
            o.projectorname AS "ProjectorName",
            o.eventid AS "EventId",
            o.effectkind AS "EffectKind",
            o.messageid AS "MessageId",
            o.eventtypename AS "EventTypeName",
            o.eventpayload AS "EventPayload",
            o.status AS "Status",
            o.attemptcount AS "AttemptCount",
            o.nextattemptatutc AS "NextAttemptAtUtc",
            o.createdatutc AS "CreatedAtUtc",
            o.publishedatutc AS "PublishedAtUtc",
            o.lasterror AS "LastError",
            o.dispatchtoken AS "DispatchToken",
            o.dispatchleaseexpiresatutc AS "DispatchLeaseExpiresAtUtc",
            o.messageoriginjson AS messageoriginjson;
        """;

    /// <summary>Reads command log rows.</summary>
    internal const string CommandAuditPostgres_MatchesStoredRetryAsync_1 =
        """
        SELECT commandpayload,commandpayloadformat,commandpayloadversion,commandpayloadsha256
        FROM command_log WHERE commandid=$1;
        """;

    /// <summary>
    /// SQL to delete an event log by event version
    /// </summary>
    internal const string DeleteEventLog =
        """
        DELETE FROM event_log
        WHERE eventversion = $1;
        """;

    /// <summary>
    /// Represents the SQL statement used to delete all records from the event log for a specified event stream ID.
    /// </summary>
    /// <remarks>The parameter $1 in the statement should be replaced with the desired event stream identifier
    /// when executing the query.</remarks>
    internal const string DeleteEventLogByStreamId =
        """
        DELETE FROM event_log
        WHERE eventstreamid = $1;
        """;

    /// <summary>
    /// SQL to delete an event name ID
    /// </summary>
    internal const string DeleteEventNameId =
        """
        DELETE FROM event_name_id
        WHERE eventname = $1
        AND eventtypename = $2;
        """;

    /// <summary>
    /// Represents the SQL statement used to delete an event stream by its unique identifier.
    /// </summary>
    /// <remarks>This constant can be used to execute a parameterized SQL command that removes a record from
    /// the event_stream_id table where the eventStreamId matches the specified value. The parameter $1 should be
    /// replaced with the desired event stream identifier when executing the statement.</remarks>
    internal const string DeleteEventStreamById =
        """
        DELETE FROM event_stream_id
        WHERE eventstreamid = $1;
        """;

    /// <summary>
    /// SQL to delete an event stream ID
    /// </summary>
    internal const string DeleteEventStreamId =
        """
        DELETE FROM event_stream_id WHERE eventstream = $1;
        """;

    /// <summary>Reads event stream id rows.</summary>
    internal const string EnlistedEventTransaction_AppendAsync_1 =
        """
        SELECT eventstreamid FROM event_stream_id WHERE eventstream=$1;
        """;

    /// <summary>Reads event name id rows.</summary>
    internal const string EnlistedEventTransaction_AppendAsync_2 =
        """
        SELECT eventnameid FROM event_name_id WHERE eventname=$1 AND eventtypename=$2;
        """;

    /// <summary>Reads and locks command log rows.</summary>
    internal const string EnlistedEventTransaction_ReserveCommandAsync_1 =
        """
        SELECT commandid FROM command_log WHERE commandid=$1 FOR UPDATE;
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string EnlistedEventTransaction_ReserveCommandAsync_2 =
        """
        SELECT EXISTS(SELECT 1 FROM event_log WHERE commandid=$1);
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string EnlistedEventTransaction_ReserveCommandAsync_3 =
        """
        SELECT EXISTS(SELECT 1 FROM event_log l JOIN event_stream_id s ON l.eventstreamid=s.eventstreamid WHERE l.commandid=$1 AND s.eventstream=$2);
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string EventSourceActorDbContext_GetCommittedStreamVersionAsync_1 =
        """
        SELECT COALESCE(MAX(streamversion),0) FROM event_log WHERE eventstreamid=$1;
        """;

    /// <summary>
    /// SQL to get command log by command ID
    /// </summary>
    internal const string GetCommandLog =
        """
        SELECT
          cl.commandid AS "CommandId",
          cl.streamid AS "StreamId",
          COALESCE(
            to_jsonb(cl) ->> 'aggregatename',
            to_jsonb(cl) ->> 'AggregateName',
            to_jsonb(cl) ->> 'actorname',
            to_jsonb(cl) ->> 'ActorName'
          ) AS "AggregateName",
          cl.commandname AS "CommandName",
          cl.commandtimestamp::timestamp AS "CommandTimestamp",
          cl.commanddata AS "CommandData",
          cl.commandpayload AS "CommandPayload",
          cl.commandpayloadformat AS "CommandPayloadFormat",
          cl.commandpayloadversion AS "CommandPayloadVersion",
          cl.commandpayloadsha256 AS "CommandPayloadSha256",
          cl.messageoriginjson AS messageoriginjson
        FROM command_log cl
        WHERE cl.commandid = $1;
        """;

    /// <summary>Finds current, stable and historical metadata rows for a typed read without rewriting them.</summary>
    internal const string GetCompatibleEventNameIds =
        """
        SELECT eventnameid, eventname, eventtypename FROM event_name_id
        WHERE eventname = $1 OR eventtypename = ANY($2);
        """;

    /// <summary>
    /// SQL to get event log by event stream ID
    /// </summary>
    internal const string GetEventLogByEventStreamId =
        """
        SELECT
                el.eventstreamid AS "EventStreamId",
                en.eventname AS "EventName",
          en.eventtypename AS "EventTypeName",
                el.eventversion AS "EventVersion",
                el.eventpayload AS "EventPayload",
                el.commandid AS "CommandId",
                el.eventtimestamp AS "EventTimeStamp",
                el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
                FROM
                event_log el JOIN event_name_id en ON el.eventnameid = en.eventnameid
            WHERE
                el.eventstreamid = $1
            ORDER BY
                el.eventversion;
        """;

    /// <summary>SQL for get event log by event version.</summary>
    internal const string GetEventLogByEventVersion =
        """
        SELECT
          el.eventstreamid AS "EventStreamId",
          en.eventname AS "EventName",
          en.eventtypename AS "EventTypeName",
          el.eventversion AS "EventVersion",
          el.eventpayload AS "EventPayload",
          el.commandid AS "CommandId",
          el.eventtimestamp AS "EventTimeStamp",
          el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
            FROM
          event_log el JOIN event_name_id en ON el.eventnameid = en.eventnameid
        WHERE
          el.eventversion = $1
        ORDER BY
          el.eventversion;
        """;

    /// <summary>
    /// SQL to get event log by max event version
    /// </summary>
    internal const string GetEventLogByMaxEventVersion =
        """
        SELECT
          el.eventstreamid AS "EventStreamId",
          en.eventname AS "EventName",
          en.eventtypename AS "EventTypeName",
          el.eventversion AS "EventVersion",
          el.eventpayload AS "EventPayload",
          el.commandid AS "CommandId",
          el.eventtimestamp AS "EventTimeStamp",
          el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
            FROM
          event_log el JOIN event_name_id en ON el.eventnameid = en.eventnameid
        WHERE
          el.eventversion >= $2 AND el.eventstreamid = $1
        ORDER BY
          el.eventversion;
        """;

    /// <summary>Reads the first committed source fact and its ordered tail at one MVCC snapshot.</summary>
    internal const string GetEventLogFromCreationSnapshot =
        """
        WITH snapshot_head AS MATERIALIZED (
            SELECT currentversion FROM event_stream_id WHERE eventstreamid = $1
        )
        SELECT el.eventstreamid, en.eventname, en.eventtypename, el.eventversion,
               el.eventpayload, el.commandid, el.eventtimestamp, el.streamversion,
               snapshot_head.currentversion AS snapshothead, el.messageoriginjson AS messageoriginjson
        FROM snapshot_head
        LEFT JOIN event_log el ON el.eventstreamid = $1
            AND el.streamversion >= 1 AND el.streamversion <= snapshot_head.currentversion
        LEFT JOIN event_name_id en ON en.eventnameid = el.eventnameid
        ORDER BY el.streamversion;
        """;

    /// <summary>Retains creation while skipping only facts superseded by an explicit full checkpoint.</summary>
    internal const string GetEventLogFromCreationWithCheckpoint =
        """
        WITH names AS (SELECT unnest($2::integer[]) AS eventnameid), snapshot_head AS MATERIALIZED (
            SELECT currentversion FROM event_stream_id WHERE eventstreamid = $1
        ), checkpoint AS MATERIALIZED (
            SELECT COALESCE(MAX(latest.streamversion),1) AS streamversion
            FROM names
            CROSS JOIN snapshot_head
            CROSS JOIN LATERAL (
                SELECT el.streamversion FROM event_log el
                WHERE el.eventstreamid=$1 AND el.eventnameid=names.eventnameid
                  AND el.streamversion<=snapshot_head.currentversion
                ORDER BY el.eventversion DESC LIMIT 1
            ) latest
        )
        SELECT el.eventstreamid, en.eventname, en.eventtypename, el.eventversion,
               el.eventpayload, el.commandid, el.eventtimestamp, el.streamversion,
               snapshot_head.currentversion AS snapshothead, checkpoint.streamversion AS checkpointversion,
               el.messageoriginjson AS messageoriginjson
        FROM snapshot_head CROSS JOIN checkpoint
        LEFT JOIN LATERAL (
            SELECT * FROM event_log creation
            WHERE creation.eventstreamid=$1 AND creation.streamversion=1
              AND creation.streamversion<=snapshot_head.currentversion
            UNION ALL
            SELECT * FROM event_log tail
            WHERE tail.eventstreamid=$1 AND tail.streamversion>1
              AND tail.streamversion>=checkpoint.streamversion
              AND tail.streamversion<=snapshot_head.currentversion
        ) el ON TRUE
        LEFT JOIN event_name_id en ON en.eventnameid=el.eventnameid
        ORDER BY el.streamversion;
        """;

    /// <summary>Resolves explicitly known checkpoint identities inside the same statement as the stream read.</summary>
    internal const string GetEventLogFromCreationWithCheckpointIdentities =
        """
        WITH names AS (SELECT eventnameid FROM event_name_id WHERE eventtypename = ANY($2)), snapshot_head AS MATERIALIZED (
            SELECT currentversion FROM event_stream_id WHERE eventstreamid = $1
        ), checkpoint AS MATERIALIZED (
            SELECT COALESCE(MAX(latest.streamversion),1) AS streamversion
            FROM names
            CROSS JOIN snapshot_head
            CROSS JOIN LATERAL (
                SELECT el.streamversion FROM event_log el
                WHERE el.eventstreamid=$1 AND el.eventnameid=names.eventnameid
                  AND el.streamversion<=snapshot_head.currentversion
                ORDER BY el.eventversion DESC LIMIT 1
            ) latest
        )
        SELECT el.eventstreamid, en.eventname, en.eventtypename, el.eventversion,
               el.eventpayload, el.commandid, el.eventtimestamp, el.streamversion,
               snapshot_head.currentversion AS snapshothead, checkpoint.streamversion AS checkpointversion,
               el.messageoriginjson AS messageoriginjson
        FROM snapshot_head CROSS JOIN checkpoint
        LEFT JOIN LATERAL (
            SELECT * FROM event_log creation
            WHERE creation.eventstreamid=$1 AND creation.streamversion=1
              AND creation.streamversion<=snapshot_head.currentversion
            UNION ALL
            SELECT * FROM event_log tail
            WHERE tail.eventstreamid=$1 AND tail.streamversion>1
              AND tail.streamversion>=checkpoint.streamversion
              AND tail.streamversion<=snapshot_head.currentversion
        ) el ON TRUE
        LEFT JOIN event_name_id en ON en.eventnameid=el.eventnameid
        ORDER BY el.streamversion;
        """;

    /// <summary>Reads creation, the latest replacement for each declared component, and the captured head without scanning superseded payloads.</summary>
    internal const string GetEventLogFromLatestComponentSnapshots =
        """
        WITH snapshot_head AS MATERIALIZED (
            SELECT currentversion FROM event_stream_id WHERE eventstreamid=$1
        ), selected AS MATERIALIZED (
            SELECT 1::bigint AS streamversion
            UNION SELECT currentversion FROM snapshot_head
            UNION
            SELECT latest.streamversion
            FROM unnest($2::integer[]) AS names(eventnameid)
            CROSS JOIN snapshot_head
            CROSS JOIN LATERAL (
                SELECT el.streamversion FROM event_log el
                WHERE el.eventstreamid=$1 AND el.eventnameid=names.eventnameid
                    AND el.streamversion<=snapshot_head.currentversion
                ORDER BY el.eventversion DESC LIMIT 1
            ) latest
        )
        SELECT el.eventstreamid,en.eventname,en.eventtypename,el.eventversion,
               el.eventpayload,el.commandid,el.eventtimestamp,el.streamversion,
               snapshot_head.currentversion AS snapshothead,el.streamversion AS checkpointversion,
               el.messageoriginjson AS messageoriginjson
        FROM snapshot_head CROSS JOIN selected
        LEFT JOIN event_log el ON el.eventstreamid=$1 AND el.streamversion=selected.streamversion
        LEFT JOIN event_name_id en ON en.eventnameid=el.eventnameid
        WHERE selected.streamversion>0
        ORDER BY selected.streamversion;
        """;

    /// <summary>
    /// Gets the latest snapshot and the last N matching events that follow it.
    /// The inner range is selected newest-first so PostgreSQL can stop after N rows;
    /// the outer query restores chronological replay order.
    /// </summary>
    internal const string GetEventLogFromSnapshotLastNRange =
        """
        WITH snapshot_versions AS MATERIALIZED (
            SELECT el.eventversion
            FROM event_log el
            WHERE el.eventstreamid = $1
              AND el.eventnameid = ANY($2)
        ),
        latest_snapshot AS (
            SELECT MAX(eventversion) AS snapshotversion FROM snapshot_versions
        ),
        last_event_range AS (
            SELECT
                el.eventstreamid,
                el.eventnameid,
                el.eventversion,
                el.streamversion,
                el.eventpayload,
                el.commandid,
                el.eventtimestamp, el.messageoriginjson AS messageoriginjson
                FROM event_log el
            CROSS JOIN latest_snapshot snapshot
            WHERE snapshot.snapshotversion IS NOT NULL
              AND el.eventstreamid = $1
              AND el.eventnameid = ANY($3)
              AND el.eventversion > snapshot.snapshotversion
            ORDER BY el.eventversion DESC
            LIMIT GREATEST($4, 0)
        ),
        replay_range AS (
            SELECT
                el.eventstreamid,
                el.eventnameid,
                el.eventversion,
                el.streamversion,
                el.eventpayload,
                el.commandid,
                el.eventtimestamp, el.messageoriginjson AS messageoriginjson
                FROM event_log el
            CROSS JOIN latest_snapshot snapshot
            WHERE snapshot.snapshotversion IS NOT NULL
              AND el.eventstreamid = $1
              AND el.eventversion = snapshot.snapshotversion

            UNION ALL

            SELECT
                eventstreamid,
                eventnameid,
                eventversion,
                streamversion,
                eventpayload,
                commandid,
                eventtimestamp, messageoriginjson AS messageoriginjson
                FROM last_event_range
        )
        SELECT
            el.eventstreamid AS "EventStreamId",
            en.eventname AS "EventName",
            en.eventtypename AS "EventTypeName",
            el.eventversion AS "EventVersion",
            el.eventpayload AS "EventPayload",
            el.commandid AS "CommandId",
            el.eventtimestamp AS "EventTimeStamp",
            el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
                FROM replay_range el
        JOIN event_name_id en ON el.eventnameid = en.eventnameid
        ORDER BY el.eventversion ASC;
        """;

    /// <summary>
    /// SQL to get last N events from event log
    /// </summary>
    internal const string GetEventLogLastNRange =
        """
        SELECT
                el.eventstreamid AS "EventStreamId",
                en.eventname AS "EventName",
          en.eventtypename AS "EventTypeName",
                el.eventversion AS "EventVersion",
                el.eventpayload AS "EventPayload",
                el.commandid AS "CommandId",
                el.eventtimestamp AS "EventTimeStamp",
                el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
                FROM
                event_log el JOIN event_name_id en ON el.eventnameid = en.eventnameid
            WHERE
                el.eventstreamid = $1
            ORDER BY
                el.eventversion DESC;
        """;

    /// <summary>
    /// Gets the last N events of one event type and restores chronological replay order.
    /// </summary>
    internal const string GetEventLogLastNRangeByEventName =
        """
        WITH last_event_range AS (
            SELECT
                el.eventstreamid,
                el.eventnameid,
                el.eventversion,
                el.streamversion,
                el.eventpayload,
                el.commandid,
                el.eventtimestamp, el.messageoriginjson AS messageoriginjson
                FROM event_log el
            WHERE el.eventstreamid = $1
              AND el.eventnameid = ANY($2)
            ORDER BY el.eventversion DESC
            LIMIT GREATEST($3, 0)
        )
        SELECT
            el.eventstreamid AS "EventStreamId",
            en.eventname AS "EventName",
            en.eventtypename AS "EventTypeName",
            el.eventversion AS "EventVersion",
            el.eventpayload AS "EventPayload",
            el.commandid AS "CommandId",
            el.eventtimestamp AS "EventTimeStamp",
            el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
                FROM last_event_range el
        JOIN event_name_id en ON el.eventnameid = en.eventnameid
        ORDER BY el.eventversion ASC;
        """;

    /// <summary>
    /// SQL to get event name ID by event name
    /// </summary>
    internal const string GetEventNameId =
        """
        SELECT
          e.eventnameid AS "EventNameId",
          e.eventname AS "EventName",
          e.eventtypename AS "EventTypeName"
        FROM
          event_name_id e
        WHERE
          e.eventname = $1
          AND e.eventtypename = $2
        ORDER BY
          e.eventnameid;
        """;

    /// <summary>SQL for get event projector execution state.</summary>
    internal const string GetEventProjectorExecutionState =
        """
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM event_projector_state
        WHERE eventid = $1 AND projectorname = $2;
        """;

    /// <summary>SQL for get event projector operational snapshot.</summary>
    internal const string GetEventProjectorOperationalSnapshot =
        """
        SELECT
            COUNT(*) FILTER (WHERE outcome IN ('Processing', 'Retrying')) AS "PendingCount",
            MIN(updatedatutc) FILTER (WHERE outcome IN ('Processing', 'Retrying')) AS "OldestPendingAtUtc",
            COUNT(*) FILTER (WHERE blockedreason <> '') AS "BlockedCount",
            COUNT(*) FILTER (WHERE outcome = 'Failed') AS "TerminalFailedCount",
            COUNT(*) FILTER (
                WHERE outcome IN ('Processing', 'Retrying')
                  AND executiontoken IS NOT NULL
                  AND leaseexpiresatutc <= $2) AS "ExpiredLeaseCount",
            (SELECT COUNT(*)
             FROM event_projector_outbox outbox
             WHERE outbox.projectorname = $1
               AND outbox.status IN ('Pending', 'Retrying', 'Publishing')) AS "OutboxPendingCount",
            (SELECT MIN(outbox.createdatutc)
             FROM event_projector_outbox outbox
             WHERE outbox.projectorname = $1
               AND outbox.status IN ('Pending', 'Retrying', 'Publishing')) AS "OldestOutboxPendingAtUtc",
            (SELECT COUNT(*)
             FROM event_projector_outbox outbox
             WHERE outbox.projectorname = $1
               AND outbox.status = 'Retrying') AS "OutboxRetryCount"
        FROM event_projector_state
        WHERE projectorname = $1;
        """;

    /// <summary>SQL for get event projector operational state page.</summary>
    internal const string GetEventProjectorOperationalStatePage =
        """
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM event_projector_state
        WHERE projectorname = $1
          AND eventid > $3
          AND (
                ($2 = 'Pending' AND outcome IN ('Processing', 'Retrying') AND blockedreason = '')
             OR ($2 = 'Failed' AND outcome = 'Failed' AND blockedreason = '')
             OR ($2 = 'Blocked' AND blockedreason <> '')
          )
        ORDER BY eventid
        LIMIT $4;
        """;

    /// <summary>SQL for get event projector recovery page.</summary>
    internal const string GetEventProjectorRecoveryPage =
        """
        SELECT
            el.eventstreamid AS "EventStreamId",
            en.eventname AS "EventName",
            en.eventtypename AS "EventTypeName",
            el.eventversion AS "EventVersion",
            el.eventpayload AS "EventPayload",
            el.commandid AS "CommandId",
            el.eventtimestamp AS "EventTimestamp",
            el.streamversion AS "StreamVersion",
            eps.eventid AS "EventId",
            eps.actorname AS "ActorName",
            eps.projectorname AS "ProjectorName",
            eps.isreplay AS "IsReplay",
            eps.attemptnumber AS "AttemptNumber",
            eps.outcome AS "Outcome",
            eps.stage AS "Stage",
            eps.errormessage AS "ErrorMessage",
            eps.createdtimestamp AS "CreatedTimestamp",
            eps.updatedtimestamp AS "UpdatedTimestamp",
            eps.eventstreamid AS "StateEventStreamId",
            eps.sourceeventname AS "SourceEventName",
            eps.revision AS "Revision",
            eps.executiontoken AS "ExecutionToken",
            eps.leaseexpiresatutc AS "LeaseExpiresAtUtc",
            eps.retrycount AS "RetryCount",
            eps.nextattemptatutc AS "NextAttemptAtUtc",
            eps.lasterroratutc AS "LastErrorAtUtc",
            eps.blockedreason AS "BlockedReason",
            eps.lastcompletedstage AS "LastCompletedStage",
            eps.updatedatutc AS "UpdatedAtUtc",
            eps.blockedstage AS "BlockedStage",
            eps.streamversion AS "StateStreamVersion", el.messageoriginjson AS messageoriginjson
        FROM event_projector_state eps
        JOIN event_log el ON el.eventversion = eps.eventid
        JOIN event_name_id en ON en.eventnameid = el.eventnameid
        WHERE eps.projectorname = $1
          AND en.eventname = ANY(string_to_array($2, ','))
          AND eps.outcome IN ('Processing', 'Retrying')
          AND eps.eventid > $3
          AND (eps.nextattemptatutc IS NULL OR eps.nextattemptatutc <= $4)
          AND (eps.executiontoken IS NULL OR eps.leaseexpiresatutc IS NULL OR eps.leaseexpiresatutc <= $4)
        ORDER BY eps.eventid
        LIMIT $5;
        """;

    /// <summary>
    /// Gets the durable state for one event/projector pair.
    /// </summary>
    internal const string GetEventProjectorState =
        """
        SELECT
                eventid AS "EventId",
                actorname AS "ActorName",
                projectorname AS "ProjectorName",
                isreplay AS "IsReplay",
                attemptnumber AS "AttemptNumber",
                outcome AS "Outcome",
                stage AS "Stage",
                errormessage AS "ErrorMessage",
                createdtimestamp AS "CreatedTimestamp",
                updatedtimestamp AS "UpdatedTimestamp"
            FROM event_projector_state
            WHERE eventid = $1 AND projectorname = $2;
        """;

    /// <summary>SQL for get event projector stream checkpoint.</summary>
    internal const string GetEventProjectorStreamCheckpoint =
        """
        SELECT projectorname AS "ProjectorName",
               eventstreamid AS "EventStreamId",
               lastappliedstreamversion AS "LastAppliedStreamVersion",
               lastappliedeventid AS "LastAppliedEventId",
               revision AS "Revision",
               updatedatutc AS "UpdatedAtUtc"
        FROM event_projector_stream_checkpoint
        WHERE projectorname = $1 AND eventstreamid = $2;
        """;

    /// <summary>
    /// SQL to get event stream ID by event stream
    /// </summary>
    internal const string GetEventStreamId =
        """
        SELECT
          e.eventstreamid AS "EventStreamId",
          e.eventstream AS "EventStream"
        FROM
          event_stream_id e
        WHERE
          e.eventstream = $1;
        """;

    /// <summary>
    /// Finds the newest compatible snapshot with one indexed seek per event-name
    /// alias. Limits apply before the aggregate so dense streams and missing
    /// snapshots cannot scan either full stream history or the global version index.
    /// </summary>
    internal const string GetMaxEventVersion =
        """
        WITH snapshot_versions AS MATERIALIZED (
            SELECT latest.eventversion
            FROM unnest($2::integer[]) AS names(eventnameid)
            CROSS JOIN LATERAL (
                SELECT el.eventversion
                FROM event_log el
                WHERE el.eventstreamid = $1
                  AND el.eventnameid = names.eventnameid
                ORDER BY el.eventversion DESC
                LIMIT 1
            ) latest
        )
        SELECT MAX(eventversion) AS "MaxEventVersion"
        FROM snapshot_versions;
        """;

    /// <summary>Checks a named resident stream without replaying source payloads.</summary>
    internal const string GetResidentStreamHead =
        """
        SELECT COALESCE((SELECT currentversion FROM event_stream_id WHERE eventstream=$1),0);
        """;

    /// <summary>Restores a stateless reducer without reading its raw tick history.</summary>
    internal const string GetStatelessEventLogFromCreationSnapshot =
        """
        WITH snapshot_head AS MATERIALIZED (
            SELECT currentversion FROM event_stream_id WHERE eventstreamid=$1
        )
        SELECT el.eventstreamid,en.eventname,en.eventtypename,el.eventversion,
               el.eventpayload,el.commandid,el.eventtimestamp,el.streamversion,
               snapshot_head.currentversion AS snapshothead,snapshot_head.currentversion AS checkpointversion,
               el.messageoriginjson AS messageoriginjson
        FROM snapshot_head
        LEFT JOIN LATERAL (
            SELECT * FROM event_log creation
            WHERE creation.eventstreamid=$1 AND creation.streamversion=1
              AND creation.streamversion<=snapshot_head.currentversion
            UNION ALL
            SELECT * FROM event_log head
            WHERE head.eventstreamid=$1 AND head.streamversion=snapshot_head.currentversion
              AND head.streamversion>1
        ) el ON TRUE
        LEFT JOIN event_name_id en ON en.eventnameid=el.eventnameid
        ORDER BY el.streamversion;
        """;

    /// <summary>
    /// Gets source events that have an explicit projector state eligible for recovery.
    /// Event-log rows without projector state and terminal completed or failed states are deliberately excluded.
    /// </summary>
    internal const string GetUncompletedEventProjectorEvents =
        """
        SELECT
                el.eventstreamid AS "EventStreamId",
                en.eventname AS "EventName",
                en.eventtypename AS "EventTypeName",
                el.eventversion AS "EventVersion",
                el.eventpayload AS "EventPayload",
                el.commandid AS "CommandId",
                el.eventtimestamp AS "EventTimestamp",
                el.streamversion AS "StreamVersion", el.messageoriginjson AS messageoriginjson
            FROM event_log el
            JOIN event_name_id en ON el.eventnameid = en.eventnameid
            JOIN event_projector_state eps
              ON eps.eventid = el.eventversion
             AND eps.projectorname = $1
            WHERE en.eventname = ANY(string_to_array($2, ','))
              AND eps.outcome IN ('Processing', 'Retrying')
            ORDER BY el.eventstreamid, el.streamversion;
        """;

    /// <summary>SQL for has committed maintenance stream.</summary>
    internal const string HasCommittedMaintenanceStream =
        """
        SELECT currentversion FROM public.event_stream_id WHERE eventstream=$1 AND currentversion>0;
        """;

    /// <summary>SQL for has committed stream with prefix.</summary>
    internal const string HasCommittedStreamWithPrefix =
        """
        SELECT EXISTS (
            SELECT 1 FROM public.event_stream_id s
            WHERE s.currentversion > 0 AND NOT (s.eventstream = ANY($1::text[]))
              AND EXISTS (SELECT 1 FROM unnest($2::text[]) p(prefix) WHERE starts_with(s.eventstream,p.prefix)));
        """;

    /// <summary>SQL for has earlier unresolved event projector execution.</summary>
    internal const string HasEarlierUnresolvedEventProjectorExecution =
        """
        SELECT EXISTS (
            SELECT 1
            FROM event_projector_state current_state
            JOIN event_projector_state earlier
              ON earlier.projectorname = current_state.projectorname
             AND earlier.eventstreamid = current_state.eventstreamid
             AND earlier.streamversion < current_state.streamversion
            WHERE current_state.eventid = $1
              AND current_state.projectorname = $2
              AND earlier.outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded')
        );
        """;

    /// <summary>SQL for has event for command.</summary>
    internal const string HasEventForCommand =
        """
        SELECT EXISTS (
            SELECT 1
            FROM event_log
            WHERE commandid = $1
        );
        """;

    /// <summary>
    /// Represents the SQL statement used to insert a new record into the command log table.
    /// </summary>
    /// <remarks>The statement uses positional parameters ($1 through $7) for parameterized query execution.
    /// Ensure that parameter values are supplied in the correct order to match the columns: CommandId, StreamId,
    /// AggregateName, CommandName, CommandTimestamp, CommandStatus, and CommandData.</remarks>
    internal const string InsertCommandLog =
        """
        INSERT INTO command_log (
                commandid,
                streamid,
                actorname,
                commandname,
                commandtimestamp,
                commandstatus,
                commanddata
            ) VALUES (
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7
            );
        """;

    /// <summary>SQL for insert command log message pack window.</summary>
    internal const string InsertCommandLogMessagePackWindow =
        """
        WITH input_raw AS (
                SELECT *
                FROM unnest(
                    $1::uuid[], $2::text[], $3::text[], $4::text[], $5::text[], $6::text[], $7::text[],
                    $8::bytea[], $9::smallint[], $10::integer[], $11::bytea[], $12::text[])
                WITH ordinality AS i(
                    commandid, streamid, actorname, commandname, commandtimestamp, commandstatus, commanddata,
                    commandpayload, commandpayloadformat, commandpayloadversion, commandpayloadsha256, messageoriginjson, ordinal)
            ),
            input AS (
                SELECT *, row_number() OVER (PARTITION BY commandid ORDER BY ordinal) AS commandoccurrence
                FROM input_raw
            ),
            inserted AS (
                INSERT INTO command_log (
                    commandid, streamid, actorname, commandname, commandtimestamp, commandstatus, commanddata,
                    commandpayload, commandpayloadformat, commandpayloadversion, commandpayloadsha256, messageoriginjson)
                SELECT commandid, streamid, actorname, commandname, commandtimestamp, commandstatus, commanddata,
                       commandpayload, commandpayloadformat, commandpayloadversion, commandpayloadsha256, messageoriginjson
                FROM input
                WHERE commandoccurrence = 1
                ORDER BY ordinal
                ON CONFLICT (commandid) DO NOTHING
                RETURNING commandid, commandpayloadsha256
            )
            SELECT i.ordinal, i.commandid,
                   (n.commandid IS NOT NULL AND i.commandoccurrence = 1) AS accepted,
                   COALESCE(n.commandpayloadsha256, c.commandpayloadsha256) AS storedpayloadsha256
            FROM input i
            LEFT JOIN inserted n ON n.commandid = i.commandid
            LEFT JOIN command_log c ON c.commandid = i.commandid
            ORDER BY i.ordinal;
        """;

    /// <summary>
    /// SQL to insert an event log
    /// </summary>
    internal const string InsertEventLog =
        """
        WITH next_stream_version AS (
            UPDATE event_stream_id
            SET currentversion = currentversion + 1
            WHERE eventstreamid = $1
            RETURNING currentversion AS streamversion
        )
        INSERT INTO event_log (
                eventstreamid,
                eventnameid,
                streamversion,
                eventpayload,
                commandid,
                eventtimestamp,
                messageoriginjson
            )
            SELECT
                $1,
                $2,
                next_stream_version.streamversion,
                $3,
                $4,
                $5,
                $6
            FROM next_stream_version
            RETURNING eventversion, streamversion;
        """;

    /// <summary>Inserts an event only when the stream is still at the caller's observed version.</summary>
    internal const string InsertEventLogExpectedVersion =
        """
        WITH next_stream_version AS (
            UPDATE event_stream_id
            SET currentversion = currentversion + 1
            WHERE eventstreamid = $1
              AND currentversion = $6
            RETURNING currentversion AS streamversion
        )
        INSERT INTO event_log (
                eventstreamid,
                eventnameid,
                streamversion,
                eventpayload,
                commandid,
                eventtimestamp,
                messageoriginjson
            )
            SELECT
                $1,
                $2,
                next_stream_version.streamversion,
                $3,
                $4,
                $5,
                $7
            FROM next_stream_version
            RETURNING eventversion;
        """;

    /// <summary>
    /// SQL to insert an event name ID
    /// </summary>
    internal const string InsertEventNameId =
        """
        INSERT INTO event_name_id (
          eventname,
          eventtypename
        ) VALUES (
          $1,
          $2
        )
        ON CONFLICT (eventname, eventtypename) DO UPDATE
        SET eventname = EXCLUDED.eventname
        RETURNING eventnameid;
        """;

    /// <summary>
    /// SQL to insert an event stream ID
    /// </summary>
    internal const string InsertEventStreamId =
        """
        INSERT INTO event_stream_id (
            eventstream
        ) VALUES (
            $1
        )
        ON CONFLICT (eventstream) DO UPDATE
        SET eventstream = EXCLUDED.eventstream
        RETURNING eventstreamid;
        """;

    /// <summary>SQL for lock aggregate checkpoint source.</summary>
    internal const string LockAggregateCheckpointSource =
        """
        SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR UPDATE;
        """;

    /// <summary>SQL for mark event projector outbox published.</summary>
    internal const string MarkEventProjectorOutboxPublished =
        """
        UPDATE event_projector_outbox
        SET status = 'Published',
            publishedatutc = $6,
            dispatchtoken = NULL,
            dispatchleaseexpiresatutc = NULL,
            nextattemptatutc = NULL,
            lasterror = ''
        WHERE projectorname = $1
          AND eventid = $2
          AND effectkind = $3
          AND dispatchtoken = $4
          AND status = 'Publishing'
          AND dispatchleaseexpiresatutc > $5;
        """;

    /// <summary>Updates business subscription projection receipt rows.</summary>
    internal const string PostgresCommittedBusinessEventJournal_CompleteHandoffAsync_1 =
        """
        UPDATE business_subscription_projection_receipt SET handoff_completed=TRUE WHERE event_id=$1;
        """;

    /// <summary>Inserts business subscription projection issue rows.</summary>
    internal const string PostgresCommittedBusinessEventJournal_RejectAsync_1 =
        """
        INSERT INTO business_subscription_projection_issue(event_id,reason_code,detail)
        VALUES($1,$2,$3) ON CONFLICT DO NOTHING;
        """;

    /// <summary>Inserts business subscription projection receipt rows.</summary>
    internal const string PostgresCommittedBusinessEventJournal_RejectAsync_2 =
        """
        INSERT INTO business_subscription_projection_receipt(event_id) VALUES($1) ON CONFLICT DO NOTHING;
        """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresQueryTimeoutScope_ApplyAsync_1 =
        """
        SELECT set_config('statement_timeout',$1,TRUE), set_config('lock_timeout',$2,TRUE);
        """;

    /// <summary>SQL for read aggregate checkpoint head.</summary>
    internal const string ReadAggregateCheckpointHead =
        """
        SELECT currentversion FROM event_stream_id WHERE eventstream=$1;
        """;

    /// <summary>SQL for read aggregate checkpoint version.</summary>
    internal const string ReadAggregateCheckpointVersion =
        """
        SELECT c.checkpoint_event_version
        FROM event_stream_id s JOIN event_aggregate_checkpoint c ON c.source_stream_id=s.eventstreamid
        WHERE s.eventstream=$1 AND c.source_revision=$2;
        """;

    /// <summary>SQL for read aggregate checkpoint window.</summary>
    internal const string ReadAggregateCheckpointWindow =
        """
        WITH source AS MATERIALIZED (
            SELECT eventstreamid, LEAST(currentversion, $3) AS head
            FROM event_stream_id WHERE eventstream=$1
        ), sidecar AS MATERIALIZED (
            SELECT eventstreamid FROM event_stream_id WHERE eventstream=$2
        ), checkpoint AS MATERIALIZED (
            SELECT c.source_revision, el.*
            FROM source s
            JOIN event_aggregate_checkpoint c ON c.source_stream_id=s.eventstreamid
            JOIN event_log el ON el.eventversion=c.checkpoint_event_version
            JOIN sidecar a ON a.eventstreamid=el.eventstreamid
            WHERE $5 < 0 AND c.source_revision<=LEAST(s.head,$4)
            ORDER BY c.source_revision DESC LIMIT 1
        ), legacy AS MATERIALIZED (
            SELECT el.*
            FROM sidecar a JOIN event_log el ON el.eventstreamid=a.eventstreamid
            WHERE $5 < 0
              AND NOT EXISTS (SELECT FROM event_aggregate_checkpoint c WHERE c.checkpoint_event_version=el.eventversion)
        ), selected AS (
            SELECT 1 AS kind, source_revision, eventversion, eventnameid, streamversion, eventpayload,
            messageoriginjson AS messageoriginjson
        FROM checkpoint
            UNION ALL
            SELECT 2, 0, eventversion, eventnameid, streamversion, eventpayload, messageoriginjson AS messageoriginjson
        FROM legacy
            UNION ALL
            SELECT 3, 0, el.eventversion, el.eventnameid, el.streamversion, el.eventpayload,
            el.messageoriginjson AS messageoriginjson
        FROM source s JOIN event_log el ON el.eventstreamid=s.eventstreamid
            WHERE (EXISTS (SELECT FROM checkpoint) OR NOT EXISTS (SELECT FROM legacy))
              AND el.streamversion>COALESCE((SELECT source_revision FROM checkpoint),GREATEST($5,0))
              AND el.streamversion<=s.head
        )
        SELECT s.head, chosen.kind, chosen.source_revision, chosen.eventversion,
               n.eventtypename, chosen.streamversion, chosen.eventpayload, chosen.messageoriginjson AS messageoriginjson
        FROM source s LEFT JOIN selected chosen ON TRUE
        LEFT JOIN event_name_id n ON n.eventnameid=chosen.eventnameid
        ORDER BY chosen.kind, chosen.streamversion;
        """;

    /// <summary>SQL for read aggregate creation.</summary>
    internal const string ReadAggregateCreation =
        """
        SELECT el.eventversion, n.eventtypename, el.streamversion, el.eventpayload, el.messageoriginjson AS messageoriginjson
        FROM event_stream_id s JOIN event_log el USING(eventstreamid) JOIN event_name_id n USING(eventnameid)
        WHERE s.eventstream=$1 AND el.streamversion=1;
        """;

    /// <summary>SQL for read aggregate event by command.</summary>
    internal const string ReadAggregateEventByCommand =
        """
        SELECT el.eventversion, n.eventtypename, el.streamversion, el.eventpayload, el.messageoriginjson AS messageoriginjson
        FROM event_log el JOIN event_stream_id s USING(eventstreamid) JOIN event_name_id n USING(eventnameid)
        WHERE el.commandid=$2 AND s.eventstream=$1
        LIMIT 2;
        """;

    /// <summary>SQL for read committed event cut.</summary>
    internal const string ReadCommittedEventCut =
        """
        SELECT COALESCE(MAX(eventversion),0) FROM event_log;
        """;

    /// <summary>SQL for read committed stream revision.</summary>
    internal const string ReadCommittedStreamRevision =
        """
        SELECT COALESCE(MAX(e.streamversion),0)
        FROM event_log e JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1;
        """;

    /// <summary>SQL for read concern checkpoint window.</summary>
    internal const string ReadConcernCheckpointWindow =
        """
        WITH streams AS MATERIALIZED (
            SELECT eventstreamid, currentversion, eventstream=$1 AS is_concern
            FROM event_stream_id WHERE eventstream IN ($1,$2)
        ), heads AS MATERIALIZED (
            SELECT COALESCE(MAX(currentversion) FILTER (WHERE is_concern),0) AS concern_head,
                   COALESCE(MAX(currentversion) FILTER (WHERE NOT is_concern),0) AS legacy_head
            FROM streams
        ), checkpoint AS MATERIALIZED (
            SELECT MAX(latest.streamversion) AS stream_version
            FROM streams s CROSS JOIN event_name_id n
            CROSS JOIN LATERAL (
                SELECT e.streamversion FROM event_log e
                WHERE e.eventstreamid=s.eventstreamid AND e.eventnameid=n.eventnameid
                  AND e.streamversion>1 AND e.streamversion<=s.currentversion
                ORDER BY e.eventversion DESC LIMIT 1
            ) latest
            WHERE $4 AND s.is_concern AND n.eventtypename=ANY($3)
        ), selected AS (
            SELECT NOT s.is_concern AS is_legacy,e.eventversion,e.eventnameid,e.streamversion,e.eventpayload,
            e.messageoriginjson AS messageoriginjson
        FROM streams s CROSS JOIN checkpoint c JOIN event_log e ON e.eventstreamid=s.eventstreamid
            WHERE e.streamversion<=s.currentversion
              AND e.streamversion>=COALESCE(c.stream_version,1)
              AND (s.is_concern OR c.stream_version IS NULL)
            UNION ALL
            SELECT FALSE,e.eventversion,e.eventnameid,e.streamversion,e.eventpayload, e.messageoriginjson AS messageoriginjson
        FROM streams s CROSS JOIN checkpoint c JOIN event_log e ON e.eventstreamid=s.eventstreamid
            WHERE s.is_concern AND c.stream_version IS NOT NULL AND e.streamversion=1
        )
        SELECT h.concern_head,h.legacy_head,COALESCE(c.stream_version,0),COALESCE(e.is_legacy,FALSE),
               COALESCE(n.eventtypename=ANY($3),FALSE),
               e.eventversion,n.eventtypename,e.streamversion,e.eventpayload, e.messageoriginjson AS messageoriginjson
        FROM heads h CROSS JOIN checkpoint c LEFT JOIN selected e ON TRUE
        LEFT JOIN event_name_id n ON n.eventnameid=e.eventnameid
        ORDER BY e.is_legacy DESC,e.streamversion;
        """;

    /// <summary>SQL for read latest committed event.</summary>
    internal const string ReadLatestCommittedEvent =
        """
        SELECT n.eventtypename,l.eventversion,l.eventpayload
        FROM event_log l
        JOIN event_name_id n USING(eventnameid)
        JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1 ORDER BY l.streamversion DESC LIMIT 1;
        """;

    /// <summary>SQL for read maintenance source events.</summary>
    internal const string ReadMaintenanceSourceEvents =
        """
        SELECT l.eventstreamid,n.eventname,n.eventtypename,l.eventversion,l.eventpayload,
               l.commandid,l.eventtimestamp,l.streamversion,s.eventstream
        FROM public.event_stream_id s
        CROSS JOIN LATERAL (SELECT * FROM public.event_log l WHERE l.eventstreamid=s.eventstreamid OFFSET 0) l
        JOIN public.event_name_id n ON n.eventnameid=l.eventnameid
        WHERE s.eventstream=ANY($1::text[])
           OR EXISTS (SELECT 1 FROM unnest($2::text[]) p(prefix) WHERE starts_with(s.eventstream,p.prefix))
        ORDER BY l.eventversion;
        """;

    const string PendingJournalStart = """
        WITH pending AS MATERIALIZED (
            SELECT eligible.eventversion
            FROM event_name_id en
            CROSS JOIN LATERAL (
                SELECT e.eventversion FROM event_log e
                WHERE e.eventnameid=en.eventnameid AND NOT EXISTS (

        """;

    // A row beyond the first 32 for its event-name ID cannot be in the global first 32.
    const string PendingJournalPerNameLimit = """

                )
                ORDER BY e.eventversion LIMIT 32
            ) eligible

        """;

    const string PendingJournalPayloads = """

            ORDER BY eligible.eventversion LIMIT 32
        )
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
               e.eventtimestamp::text,e.streamversion
        FROM pending p JOIN event_log e ON e.eventversion=p.eventversion
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        ORDER BY e.eventversion;
        """;

    /// <summary>Reads the oldest business events without a receipt.</summary>
    internal const string ReadPendingBusinessEvents = PendingJournalStart
        + "SELECT 1 FROM business_subscription_projection_receipt r WHERE r.event_id=e.eventversion"
        + PendingJournalPerNameLimit + "WHERE en.eventname=ANY($1)" + PendingJournalPayloads;

    /// <summary>SQL for read pending business handoffs.</summary>
    internal const string ReadPendingBusinessHandoffs =
        """
        SELECT el.eventstreamid,en.eventname,en.eventtypename,el.eventversion,el.eventpayload,el.commandid,
            el.eventtimestamp::text,el.streamversion
        FROM event_log el
        JOIN event_name_id en ON en.eventnameid=el.eventnameid
        JOIN business_subscription_projection_receipt r ON r.event_id=el.eventversion
        WHERE en.eventname='WorkflowStrategyStateUpdatedEvent' AND NOT r.handoff_completed
        ORDER BY el.eventversion LIMIT 32;
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string ReadPendingRiskJournalEventsByName = PendingJournalStart
        + "SELECT 1 FROM risk_history_projection_receipt r WHERE r.event_id=e.eventversion AND r.projection_name=$2"
        + PendingJournalPerNameLimit + "WHERE en.eventname=$1" + PendingJournalPayloads;

    /// <summary>SQL for read prior business event.</summary>
    internal const string ReadPriorBusinessEvent =
        """
        SELECT el.eventstreamid,en.eventname,en.eventtypename,el.eventversion,el.eventpayload,el.commandid,
            el.eventtimestamp::text,el.streamversion
        FROM event_log el
        JOIN event_name_id en ON en.eventnameid=el.eventnameid
        WHERE el.eventstreamid=$1 AND el.eventversion<=$2 AND en.eventname=ANY($3)
        ORDER BY el.eventversion DESC LIMIT 1;
        """;

    /// <summary>SQL for register aggregate checkpoint.</summary>
    internal const string RegisterAggregateCheckpoint =
        """
        INSERT INTO event_aggregate_checkpoint(source_stream_id,source_revision,checkpoint_event_version)
        SELECT eventstreamid,$2,$3 FROM event_stream_id WHERE eventstream=$1;
        """;

    /// <summary>SQL for release event projector outbox.</summary>
    internal const string ReleaseEventProjectorOutbox =
        """
        UPDATE event_projector_outbox
        SET status = $6,
            nextattemptatutc = $7,
            lasterror = $8,
            dispatchtoken = NULL,
            dispatchleaseexpiresatutc = NULL
        WHERE projectorname = $1
          AND eventid = $2
          AND effectkind = $3
          AND dispatchtoken = $4
          AND status = 'Publishing'
          AND dispatchleaseexpiresatutc > $5;
        """;

    /// <summary>SQL for reserve copied stream versions.</summary>
    internal const string ReserveCopiedStreamVersions =
        """
        UPDATE event_stream_id AS stream
        SET currentversion = updates.finalversion
        FROM unnest($1::bigint[], $2::bigint[]) AS updates(eventstreamid, finalversion)
        WHERE stream.eventstreamid = updates.eventstreamid;
        """;

    /// <summary>Inserts risk history projection receipt rows.</summary>
    internal const string RiskHistoryJournal_AcknowledgeAsync_1 =
        """
        INSERT INTO risk_history_projection_receipt(projection_name,event_id)
        VALUES($1,$2) ON CONFLICT DO NOTHING;
        """;

    /// <summary>Reads risk history projection progress rows.</summary>
    internal const string RiskHistoryJournal_LoadCursorAsync_1 =
        """
        SELECT after_event_version FROM risk_history_projection_progress WHERE projection_name=$1;
        """;

    /// <summary>Inserts risk history projection receipt rows.</summary>
    internal const string RiskHistoryJournal_LoadCursorAsync_2 =
        """
        INSERT INTO risk_history_projection_receipt(projection_name,event_id)
        SELECT $1,e.eventversion
        FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE n.eventname='WorkflowStrategyStateUpdatedEvent' AND e.eventversion<=$2
        ON CONFLICT DO NOTHING;
        """;

    /// <summary>Reads event log rows.</summary>
    internal const string RiskHistoryJournal_PageAsync_1 = PendingJournalStart
        + "SELECT 1 FROM risk_history_projection_receipt r WHERE r.event_id=e.eventversion AND r.projection_name=$1"
        + PendingJournalPerNameLimit + "WHERE en.eventname='WorkflowStrategyStateUpdatedEvent'" + PendingJournalPayloads;

    /// <summary>Inserts risk history projection issue rows.</summary>
    internal const string RiskHistoryJournal_QuarantineConflictAsync_1 =
        """
        INSERT INTO risk_history_projection_issue(
          projection_name,event_id,workflow_id,invocation_id,source_revision,issue_code,stored_hash,incoming_hash)
        VALUES($1,$2,$3,$4,$5,'ImmutableRevisionConflict',$6,$7)
        ON CONFLICT DO NOTHING;
        """;

    /// <summary>Inserts risk history projection progress rows.</summary>
    internal const string RiskHistoryJournal_SaveCursorAsync_1 =
        """
        INSERT INTO risk_history_projection_progress(projection_name,after_event_version) VALUES($2,$1)
        ON CONFLICT(projection_name) DO UPDATE
        SET after_event_version=GREATEST(risk_history_projection_progress.after_event_version,EXCLUDED.after_event_version);
        """;

    /// <summary>SQL for try claim event projector execution.</summary>
    internal const string TryClaimEventProjectorExecution =
        """
        WITH reconciled AS (
            UPDATE event_projector_state current_state
            SET stage = 'Completed',
                outcome = CASE
                    WHEN checkpoint.lastappliedstreamversion = current_state.streamversion
                        THEN 'AlreadyCompleted'
                    ELSE 'Superseded'
                END,
                errormessage = 'stream-checkpoint-covered',
                blockedreason = 'stream-checkpoint-covered',
                executiontoken = NULL,
                leaseexpiresatutc = NULL,
                revision = current_state.revision + 1 /* Revision = Revision + 1 */,
                updatedatutc = $5,
                updatedtimestamp = $6
            FROM event_projector_stream_checkpoint checkpoint
            WHERE current_state.eventid = $1
              AND current_state.projectorname = $2
              AND checkpoint.projectorname = current_state.projectorname
              AND checkpoint.eventstreamid = current_state.eventstreamid
              AND checkpoint.lastappliedstreamversion >= current_state.streamversion
              AND current_state.outcome IN ('Processing', 'Retrying')
              AND current_state.stage IN ('ValidateSourceEvent', 'PublishProcessingEvent', 'ApplyProjection')
            RETURNING current_state.*
        ), claimed AS (
            UPDATE event_projector_state current_state
            SET executiontoken = $3,
                leaseexpiresatutc = $4,
                revision = current_state.revision + 1 /* Revision = Revision + 1 */,
                updatedatutc = $5,
                updatedtimestamp = $6
            WHERE current_state.eventid = $1
              AND current_state.projectorname = $2
              AND current_state.outcome IN ('Processing', 'Retrying')
              AND NOT EXISTS (SELECT 1 FROM reconciled)
              AND (current_state.executiontoken IS NULL OR current_state.leaseexpiresatutc IS NULL OR current_state.leaseexpiresatutc <= $5)
              AND NOT EXISTS (
                  SELECT 1
                  FROM event_projector_state earlier
                  WHERE earlier.projectorname = current_state.projectorname
                    AND earlier.eventstreamid = current_state.eventstreamid
                    AND earlier.streamversion < current_state.streamversion
                    AND earlier.outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded')
              )
            RETURNING current_state.*
        )
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM reconciled
        UNION ALL
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM claimed
        LIMIT 1;
        """;

    /// <summary>SQL for try create event projector execution state.</summary>
    internal const string TryCreateEventProjectorExecutionState =
        """
        INSERT INTO event_projector_state (
            eventid, actorname, projectorname, isreplay, attemptnumber,
            outcome, stage, errormessage, createdtimestamp, updatedtimestamp,
            eventstreamid, sourceeventname, updatedatutc, streamversion
        )
        SELECT
            $1, $2, $3, $4, $5,
            CASE
                WHEN checkpoint.lastappliedstreamversion IS NULL
                  OR checkpoint.lastappliedstreamversion < el.streamversion THEN $6
                WHEN checkpoint.lastappliedstreamversion = el.streamversion THEN 'AlreadyCompleted'
                ELSE 'Superseded'
            END,
            CASE WHEN checkpoint.lastappliedstreamversion >= el.streamversion THEN 'Completed' ELSE $7 END,
            CASE
                WHEN checkpoint.lastappliedstreamversion >= el.streamversion
                    THEN 'stream-checkpoint-covered'
                ELSE $8
            END,
            $9, $10,
            el.eventstreamid, en.eventname, $11, el.streamversion
        FROM event_log el
        JOIN event_name_id en ON en.eventnameid = el.eventnameid
        LEFT JOIN event_projector_stream_checkpoint checkpoint
          ON checkpoint.projectorname = $3
         AND checkpoint.eventstreamid = el.eventstreamid
        WHERE el.eventversion = $1
        ON CONFLICT (eventid, projectorname) DO NOTHING
        RETURNING    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion";
        """;

    /// <summary>SQL for try insert command log.</summary>
    internal const string TryInsertCommandLog =
        """
        WITH inserted AS (
                INSERT INTO command_log (
                    commandid,
                    streamid,
                    actorname,
                    commandname,
                    commandtimestamp,
                    commandstatus,
                    commanddata
                ) VALUES ($1, $2, $3, $4, $5, $6, $7)
                ON CONFLICT (commandid) DO NOTHING
                RETURNING commandid
            )
            SELECT EXISTS (SELECT 1 FROM inserted);
        """;

    /// <summary>SQL for try release event projector execution.</summary>
    internal const string TryReleaseEventProjectorExecution =
        """
        UPDATE event_projector_state
        SET executiontoken = NULL,
            leaseexpiresatutc = NULL,
            outcome = 'Retrying',
            retrycount = $7,
            nextattemptatutc = $8,
            lasterroratutc = $9,
            errormessage = $10,
            revision = revision + 1,
            updatedatutc = $11,
            updatedtimestamp = $12
        WHERE eventid = $1
          AND projectorname = $2
          AND executiontoken = $3
          AND revision = $4
          AND stage = $5
          AND leaseexpiresatutc > $6
          AND outcome IN ('Processing', 'Retrying')
        RETURNING    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion";
        """;

    /// <summary>SQL for try renew event projector execution.</summary>
    internal const string TryRenewEventProjectorExecution =
        """
        UPDATE event_projector_state
        SET leaseexpiresatutc = $6,
            revision = revision + 1,
            updatedatutc = $5,
            updatedtimestamp = $7
        WHERE eventid = $1
          AND projectorname = $2
          AND executiontoken = $3
          AND revision = $4
          AND leaseexpiresatutc > $5
          AND outcome IN ('Processing', 'Retrying')
        RETURNING    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion";
        """;

    /// <summary>SQL for try retry event projector execution.</summary>
    internal const string TryRetryEventProjectorExecution =
        """
        UPDATE event_projector_state
        SET stage = blockedstage,
            blockedstage = 'None',
            outcome = 'Retrying',
            attemptnumber = 0,
            retrycount = 0,
            nextattemptatutc = NULL,
            lasterroratutc = NULL,
            errormessage = '',
            blockedreason = '',
            executiontoken = NULL,
            leaseexpiresatutc = NULL,
            revision = revision + 1,
            updatedatutc = $3,
            updatedtimestamp = $4
        WHERE eventid = $1
          AND projectorname = $2
          AND stage = 'Completed'
          AND blockedstage NOT IN ('None', 'Completed')
          AND (outcome = 'Failed' OR (outcome = 'Superseded' AND blockedreason LIKE 'operator-skip:%'))
        RETURNING    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion";
        """;

    /// <summary>SQL for try skip event projector execution.</summary>
    internal const string TrySkipEventProjectorExecution =
        """
        UPDATE event_projector_state
        SET stage = 'Completed',
            blockedstage = stage,
            outcome = 'Superseded',
            nextattemptatutc = NULL,
            lasterroratutc = $4,
            errormessage = $3,
            blockedreason = 'operator-skip:' || $3,
            executiontoken = NULL,
            leaseexpiresatutc = NULL,
            revision = revision + 1,
            updatedatutc = $4,
            updatedtimestamp = $5
        WHERE eventid = $1
          AND projectorname = $2
          AND outcome NOT IN ('Completed', 'Superseded', 'AlreadyCompleted')
        RETURNING    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion";
        """;

    /// <summary>SQL for try terminalize event projector execution.</summary>
    internal const string TryTerminalizeEventProjectorExecution =
        """
        WITH transitioned AS (
            UPDATE event_projector_state
            SET stage = 'Completed',
                blockedstage = CASE WHEN $7 = 'Failed' THEN stage ELSE 'None' END,
                outcome = $7,
                lastcompletedstage = $8,
                retrycount = $9,
                nextattemptatutc = NULL,
                lasterroratutc = $10,
                errormessage = $11,
                blockedreason = $12,
                executiontoken = NULL,
                leaseexpiresatutc = NULL,
                revision = revision + 1,
                updatedatutc = $13,
                updatedtimestamp = $14
            WHERE eventid = $1
              AND projectorname = $2
              AND executiontoken = $3
              AND revision = $4
              AND stage = $5
              AND leaseexpiresatutc > $6
              AND outcome IN ('Processing', 'Retrying')
            RETURNING *
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                projectorname, eventstreamid, lastappliedstreamversion,
                lastappliedeventid, revision, updatedatutc)
            SELECT projectorname, eventstreamid, streamversion, eventid, 0, $13
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 = 'Completed'
            ON CONFLICT (projectorname, eventstreamid) DO UPDATE SET
                lastappliedstreamversion = EXCLUDED.lastappliedstreamversion,
                lastappliedeventid = EXCLUDED.lastappliedeventid,
                revision = event_projector_stream_checkpoint.revision + 1,
                updatedatutc = EXCLUDED.updatedatutc
            WHERE event_projector_stream_checkpoint.lastappliedstreamversion < EXCLUDED.lastappliedstreamversion
        )
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM transitioned;
        """;

    /// <summary>SQL for try terminalize event projector execution with outbox.</summary>
    internal const string TryTerminalizeEventProjectorExecutionWithOutbox =
        """
        WITH transitioned AS (
            UPDATE event_projector_state
            SET stage = 'Completed',
                blockedstage = CASE WHEN $7 = 'Failed' THEN stage ELSE 'None' END,
                outcome = $7,
                lastcompletedstage = $8,
                retrycount = $9,
                nextattemptatutc = NULL,
                lasterroratutc = $10,
                errormessage = $11,
                blockedreason = $12,
                executiontoken = NULL,
                leaseexpiresatutc = NULL,
                revision = revision + 1,
                updatedatutc = $13,
                updatedtimestamp = $14
            WHERE eventid = $1
              AND projectorname = $2
              AND executiontoken = $3
              AND revision = $4
              AND stage = $5
              AND leaseexpiresatutc > $6
              AND outcome IN ('Processing', 'Retrying')
            RETURNING *
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                projectorname, eventstreamid, lastappliedstreamversion,
                lastappliedeventid, revision, updatedatutc)
            SELECT projectorname, eventstreamid, streamversion, eventid, 0, $13
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 = 'Completed'
            ON CONFLICT (projectorname, eventstreamid) DO UPDATE SET
                lastappliedstreamversion = EXCLUDED.lastappliedstreamversion,
                lastappliedeventid = EXCLUDED.lastappliedeventid,
                revision = event_projector_stream_checkpoint.revision + 1,
                updatedatutc = EXCLUDED.updatedatutc
            WHERE event_projector_stream_checkpoint.lastappliedstreamversion < EXCLUDED.lastappliedstreamversion
        ), staged AS (
            INSERT INTO event_projector_outbox (
                projectorname, eventid, effectkind, messageid, eventtypename,
                eventpayload, status, attemptcount, nextattemptatutc, createdatutc, lasterror, messageoriginjson)
            SELECT projectorname, eventid, $15, $16, $17,
                   $18, 'Pending', 0, $19, $19, '', $20
            FROM transitioned
            ON CONFLICT (projectorname, eventid, effectkind) DO NOTHING
        )
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM transitioned;
        """;

    /// <summary>SQL for try transition event projector execution.</summary>
    internal const string TryTransitionEventProjectorExecution =
        """
        WITH transitioned AS (
        UPDATE event_projector_state
        SET stage = $7,
            outcome = $8,
            lastcompletedstage = $9,
            retrycount = $10,
            nextattemptatutc = $11,
            lasterroratutc = $12,
            errormessage = $13,
            blockedreason = $14,
            revision = revision + 1,
            updatedatutc = $15,
            updatedtimestamp = $16
        WHERE eventid = $1
          AND projectorname = $2
          AND executiontoken = $3
          AND revision = $4
          AND stage = $5
          AND leaseexpiresatutc > $6
          AND outcome IN ('Processing', 'Retrying')
        RETURNING *
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                projectorname, eventstreamid, lastappliedstreamversion,
                lastappliedeventid, revision, updatedatutc)
            SELECT projectorname, eventstreamid, streamversion, eventid, 0, $15
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 <> 'PublishFailedEvent'
            ON CONFLICT (projectorname, eventstreamid) DO UPDATE SET
                lastappliedstreamversion = EXCLUDED.lastappliedstreamversion,
                lastappliedeventid = EXCLUDED.lastappliedeventid,
                revision = event_projector_stream_checkpoint.revision + 1,
                updatedatutc = EXCLUDED.updatedatutc
            WHERE event_projector_stream_checkpoint.lastappliedstreamversion < EXCLUDED.lastappliedstreamversion
        )
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM transitioned;
        """;

    /// <summary>SQL for try transition event projector execution with outbox.</summary>
    internal const string TryTransitionEventProjectorExecutionWithOutbox =
        """
        WITH transitioned AS (
            UPDATE event_projector_state
            SET stage = $7,
                outcome = $8,
                lastcompletedstage = $9,
                retrycount = $10,
                nextattemptatutc = $11,
                lasterroratutc = $12,
                errormessage = $13,
                blockedreason = $14,
                revision = revision + 1,
                updatedatutc = $15,
                updatedtimestamp = $16
            WHERE eventid = $1
              AND projectorname = $2
              AND executiontoken = $3
              AND revision = $4
              AND stage = $5
              AND leaseexpiresatutc > $6
              AND outcome IN ('Processing', 'Retrying')
            RETURNING *
        ), checkpointed AS (
            INSERT INTO event_projector_stream_checkpoint (
                projectorname, eventstreamid, lastappliedstreamversion,
                lastappliedeventid, revision, updatedatutc)
            SELECT projectorname, eventstreamid, streamversion, eventid, 0, $15
            FROM transitioned
            WHERE $5 = 'ApplyProjection' AND $7 <> 'PublishFailedEvent'
            ON CONFLICT (projectorname, eventstreamid) DO UPDATE SET
                lastappliedstreamversion = EXCLUDED.lastappliedstreamversion,
                lastappliedeventid = EXCLUDED.lastappliedeventid,
                revision = event_projector_stream_checkpoint.revision + 1,
                updatedatutc = EXCLUDED.updatedatutc
            WHERE event_projector_stream_checkpoint.lastappliedstreamversion < EXCLUDED.lastappliedstreamversion
        ), staged AS (
            INSERT INTO event_projector_outbox (
                projectorname, eventid, effectkind, messageid, eventtypename,
                eventpayload, status, attemptcount, nextattemptatutc, createdatutc, lasterror, messageoriginjson)
            SELECT projectorname, eventid, $17, $18, $19,
                   $20, 'Pending', 0, $21, $21, '', $22
            FROM transitioned
            ON CONFLICT (projectorname, eventid, effectkind) DO NOTHING
        )
        SELECT    eventid AS "EventId",
            actorname AS "ActorName",
            projectorname AS "ProjectorName",
            isreplay AS "IsReplay",
            attemptnumber AS "AttemptNumber",
            outcome AS "Outcome",
            stage AS "Stage",
            errormessage AS "ErrorMessage",
            createdtimestamp AS "CreatedTimestamp",
            updatedtimestamp AS "UpdatedTimestamp",
            eventstreamid AS "EventStreamId",
            sourceeventname AS "SourceEventName",
            revision AS "Revision",
            executiontoken AS "ExecutionToken",
            leaseexpiresatutc AS "LeaseExpiresAtUtc",
            retrycount AS "RetryCount",
            nextattemptatutc AS "NextAttemptAtUtc",
            lasterroratutc AS "LastErrorAtUtc",
            blockedreason AS "BlockedReason",
            lastcompletedstage AS "LastCompletedStage",
            updatedatutc AS "UpdatedAtUtc",
            blockedstage AS "BlockedStage",
            streamversion AS "StreamVersion"
        FROM transitioned;
        """;

    /// <summary>
    /// Represents the SQL statement used to update the status of a command in the command log table.
    /// </summary>
    /// <remarks>The SQL statement uses positional parameters for the command ID and the new status value.
    /// Ensure that parameter values are supplied in the correct order when executing this command.</remarks>
    internal const string UpdateCommandLog =
        """
        UPDATE command_log
            SET commandstatus = $2,
                commandtimestamp = $3
            WHERE commandid = $1;
        """;

    /// <summary>SQL for update event log.</summary>
    internal const string UpdateEventLog =
        """
        UPDATE event_log SET
            eventpayload = $1,
            commandid = $2,
            eventtimestamp = $3
        WHERE
            eventstreamid = $4 AND
            eventnameid = $5 AND
            eventversion = $6
        RETURNING eventversion;
        """;

    /// <summary>
    /// Inserts or updates durable state for a single event/projector pair.
    /// </summary>
    internal const string UpsertEventProjectorState =
        """
        INSERT INTO event_projector_state (
                eventid,
                actorname,
                projectorname,
                isreplay,
                attemptnumber,
                outcome,
                stage,
                errormessage,
                createdtimestamp,
                updatedtimestamp,
                eventstreamid,
                sourceeventname,
                updatedatutc,
                streamversion
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
                el.eventstreamid,
                en.eventname,
                CURRENT_TIMESTAMP,
                el.streamversion
            FROM event_log el
            JOIN event_name_id en ON en.eventnameid = el.eventnameid
            WHERE el.eventversion = $1
            ON CONFLICT (eventid, projectorname) DO UPDATE SET
                actorname = EXCLUDED.actorname,
                isreplay = EXCLUDED.isreplay,
                attemptnumber = EXCLUDED.attemptnumber,
                outcome = EXCLUDED.outcome,
                stage = EXCLUDED.stage,
                errormessage = EXCLUDED.errormessage,
                updatedtimestamp = EXCLUDED.updatedtimestamp,
                eventstreamid = COALESCE(event_projector_state.eventstreamid, EXCLUDED.eventstreamid),
                sourceeventname = CASE
                    WHEN event_projector_state.sourceeventname = '' THEN EXCLUDED.sourceeventname
                    ELSE event_projector_state.sourceeventname
                END,
                streamversion = COALESCE(event_projector_state.streamversion, EXCLUDED.streamversion),
                updatedatutc = CURRENT_TIMESTAMP;
        """;
}
