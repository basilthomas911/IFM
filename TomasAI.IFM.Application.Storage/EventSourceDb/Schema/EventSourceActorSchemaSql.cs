namespace TomasAI.IFM.Application.Storage.EventSourceDb.Schema;

/// <summary>Contains PostgreSQL schema statements for EventSourceDb.</summary>
public static class EventSourceActorSchemaSql
{
    /// <summary>Adds the versioned, uncompressed MessagePack command payload without rewriting immutable legacy JSON rows.</summary>
    internal const string AddCommandLogMessagePackPayload =
        """
        ALTER TABLE command_log
            ADD COLUMN IF NOT EXISTS CommandPayload bytea NULL,
            ADD COLUMN IF NOT EXISTS CommandPayloadFormat smallint NULL,
            ADD COLUMN IF NOT EXISTS CommandPayloadVersion integer NULL,
            ADD COLUMN IF NOT EXISTS CommandPayloadSha256 bytea NULL;

        ALTER TABLE command_log
            DROP CONSTRAINT IF EXISTS ck_command_log_payload_format;
        ALTER TABLE command_log
            ADD CONSTRAINT ck_command_log_payload_format CHECK (
                (CommandPayload IS NULL AND CommandPayloadFormat IS NULL AND CommandPayloadVersion IS NULL AND CommandPayloadSha256 IS NULL)
                OR
                (CommandPayload IS NOT NULL AND CommandPayloadFormat = 1 AND CommandPayloadVersion > 0
                    AND CommandPayloadSha256 IS NOT NULL AND octet_length(CommandPayloadSha256) = 32)
            );
        """;

    /// <summary>Adds the message origin metadata.</summary>
    internal const string AddMessageOriginMetadata =
        """
        ALTER TABLE public.event_log ADD COLUMN IF NOT EXISTS MessageOriginJson text;
        ALTER TABLE public.event_projector_outbox ADD COLUMN IF NOT EXISTS MessageOriginJson text;
        ALTER TABLE public.command_log ADD COLUMN IF NOT EXISTS MessageOriginJson text;
        """;

    /// <summary>Creates the aggregate checkpoint.</summary>
    internal const string CreateAggregateCheckpoint =
        """
        CREATE TABLE IF NOT EXISTS public.event_aggregate_checkpoint (
            source_stream_id bigint NOT NULL REFERENCES public.event_stream_id(EventStreamId) ON DELETE CASCADE,
            source_revision bigint NOT NULL CHECK (source_revision > 0),
            checkpoint_event_version bigint NOT NULL REFERENCES public.event_log(EventVersion) ON DELETE CASCADE,
            PRIMARY KEY (source_stream_id, source_revision),
            UNIQUE (checkpoint_event_version)
        );
        """;

    /// <summary>Creates the business subscription journal.</summary>
    internal const string CreateBusinessSubscriptionJournal =
        """
        CREATE TABLE IF NOT EXISTS business_subscription_projection_receipt (
            event_id bigint PRIMARY KEY REFERENCES event_log(eventVersion) ON DELETE CASCADE,
            projected_at_utc timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE business_subscription_projection_receipt ADD COLUMN IF NOT EXISTS handoff_completed boolean NOT NULL DEFAULT false;
        CREATE INDEX IF NOT EXISTS business_subscription_receipt_pending_handoff
            ON business_subscription_projection_receipt (event_id) WHERE NOT handoff_completed;
        CREATE TABLE IF NOT EXISTS business_subscription_projection_issue (
            event_id bigint PRIMARY KEY REFERENCES event_log(eventVersion) ON DELETE CASCADE,
            reason_code text NOT NULL,
            detail text NOT NULL,
            rejected_at_utc timestamptz NOT NULL DEFAULT now()
        );
        """;

    /// <summary>Creates the command log.</summary>
    internal const string CreateCommandLog =
        """
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

    /// <summary>Creates the authoritative event log.</summary>
    internal const string CreateEventLogTable =
        """
        CREATE TABLE IF NOT EXISTS public.event_log (
            EventStreamId bigint NOT NULL,
            EventNameId integer NOT NULL,
            EventVersion bigint DEFAULT nextval('public.event_log_eventversion_seq'::regclass) NOT NULL,
            StreamVersion bigint NOT NULL,
            EventPayload bytea NOT NULL CHECK (octet_length(EventPayload) > 0),
            CommandId uuid NOT NULL,
            EventTimestamp text NOT NULL,
            MessageOriginJson text,
            CONSTRAINT event_log_pkey PRIMARY KEY (EventStreamId, StreamVersion)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_event_version
        ON public.event_log (EventVersion);

        CREATE INDEX IF NOT EXISTS ix_event_log_command_id
        ON public.event_log (CommandId);

        CREATE INDEX IF NOT EXISTS ix_event_log_event_name_version
        ON public.event_log (EventNameId, EventVersion);
        """;

    /// <summary>Creates the event name id sequence.</summary>
    internal const string CreateEventNameIdSequence =
        """
        CREATE SEQUENCE IF NOT EXISTS public.event_name_id_eventnameid_seq;
        """;

    /// <summary>Creates the event name id table.</summary>
    internal const string CreateEventNameIdTable =
        """
        CREATE TABLE IF NOT EXISTS public.event_name_id (
            EventNameId integer DEFAULT nextval('public.event_name_id_eventnameid_seq'::regclass) NOT NULL,
            EventName varchar(64) NOT NULL,
            EventTypeName varchar(512) NOT NULL,
            CONSTRAINT event_name_id_pkey PRIMARY KEY (EventNameId),
            CONSTRAINT event_name_id_eventname_fulleventname_key UNIQUE (EventName, EventTypeName)
        );
        """;

    /// <summary>Creates the event projector outbox v2.</summary>
    internal const string CreateEventProjectorOutboxV2 =
        """
        CREATE TABLE IF NOT EXISTS event_projector_outbox (
            ProjectorName varchar(255) NOT NULL,
            EventId bigint NOT NULL,
            EffectKind varchar(50) NOT NULL,
            MessageId varchar(128) NOT NULL,
            EventTypeName text NOT NULL,
            EventPayload bytea NOT NULL,
            Status varchar(50) NOT NULL DEFAULT 'Pending',
            AttemptCount integer NOT NULL DEFAULT 0,
            NextAttemptAtUtc timestamptz,
            CreatedAtUtc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PublishedAtUtc timestamptz,
            LastError text NOT NULL DEFAULT '',
            DispatchToken uuid,
            DispatchLeaseExpiresAtUtc timestamptz,
            CONSTRAINT pk_event_projector_outbox PRIMARY KEY (ProjectorName, EventId, EffectKind),
            CONSTRAINT fk_event_projector_outbox_state FOREIGN KEY (EventId, ProjectorName) REFERENCES event_projector_state(EventId, ProjectorName) ON DELETE CASCADE,
            CONSTRAINT ux_event_projector_outbox_message_id UNIQUE (MessageId)
        );

        ALTER TABLE event_projector_outbox
            ADD COLUMN IF NOT EXISTS DispatchToken uuid,
            ADD COLUMN IF NOT EXISTS DispatchLeaseExpiresAtUtc timestamptz;

        CREATE INDEX IF NOT EXISTS ix_event_projector_outbox_pending_v2
            ON event_projector_outbox (Status, NextAttemptAtUtc, CreatedAtUtc)
            WHERE Status IN ('Pending', 'Retrying');

        CREATE INDEX IF NOT EXISTS ix_event_projector_outbox_dispatch_lease_v2
            ON event_projector_outbox (Status, DispatchLeaseExpiresAtUtc)
            WHERE Status = 'Publishing';

        CREATE INDEX IF NOT EXISTS ix_event_projector_outbox_projector_pending
            ON event_projector_outbox (ProjectorName, NextAttemptAtUtc)
            WHERE Status IN ('Pending', 'Retrying');

        CREATE INDEX IF NOT EXISTS ix_event_projector_outbox_projector_lease
            ON event_projector_outbox (ProjectorName, DispatchLeaseExpiresAtUtc)
            WHERE Status = 'Publishing';
        """;

    /// <summary>Creates projector state keyed by event and projector.</summary>
    internal const string CreateEventProjectorState =
        """
        CREATE TABLE IF NOT EXISTS event_projector_state (
            EventId bigint NOT NULL,
            ActorName varchar(255) NOT NULL,
            ProjectorName varchar(255) NOT NULL,
            IsReplay boolean NOT NULL,
            AttemptNumber integer NOT NULL,
            Outcome varchar(50) NOT NULL,
            Stage varchar(50) NOT NULL,
            ErrorMessage text NOT NULL DEFAULT '',
            CreatedTimestamp text NOT NULL,
            UpdatedTimestamp text NOT NULL,
            CONSTRAINT pk_event_projector_state PRIMARY KEY (EventId, ProjectorName),
            CONSTRAINT fk_event_projector_state_event_log FOREIGN KEY (EventId) REFERENCES event_log(EventVersion) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_event_projector_state_projector_outcome
        ON event_projector_state (ProjectorName, Outcome);
        """;

    /// <summary>Creates the event projector state reliability v2.</summary>
    internal const string CreateEventProjectorStateReliabilityV2 =
        """
        ALTER TABLE event_projector_state
            ADD COLUMN IF NOT EXISTS EventStreamId bigint,
            ADD COLUMN IF NOT EXISTS SourceEventName varchar(255) NOT NULL DEFAULT '',
            ADD COLUMN IF NOT EXISTS Revision bigint NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS ExecutionToken uuid,
            ADD COLUMN IF NOT EXISTS LeaseExpiresAtUtc timestamptz,
            ADD COLUMN IF NOT EXISTS RetryCount integer NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS NextAttemptAtUtc timestamptz,
            ADD COLUMN IF NOT EXISTS LastErrorAtUtc timestamptz,
            ADD COLUMN IF NOT EXISTS BlockedReason text NOT NULL DEFAULT '',
            ADD COLUMN IF NOT EXISTS LastCompletedStage varchar(50) NOT NULL DEFAULT 'None',
            ADD COLUMN IF NOT EXISTS BlockedStage varchar(50) NOT NULL DEFAULT 'None',
            ADD COLUMN IF NOT EXISTS UpdatedAtUtc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP;

        UPDATE event_projector_state eps
        SET EventStreamId = el.EventStreamId,
            SourceEventName = en.EventName
        FROM event_log el
        JOIN event_name_id en ON en.EventNameId = el.EventNameId
        WHERE eps.EventId = el.EventVersion
          AND (eps.EventStreamId IS NULL OR eps.SourceEventName = '');

        ALTER TABLE event_projector_state
            ALTER COLUMN EventStreamId SET NOT NULL;

        CREATE INDEX IF NOT EXISTS ix_event_projector_state_pending_v2
            ON event_projector_state (ProjectorName, EventId)
            WHERE Outcome IN ('Processing', 'Retrying');

        CREATE INDEX IF NOT EXISTS ix_event_projector_state_stream_pending_v2
            ON event_projector_state (ProjectorName, EventStreamId, EventId)
            WHERE Outcome IN ('Processing', 'Retrying');
        """;

    /// <summary>Creates the event stream id sequence.</summary>
    internal const string CreateEventStreamIdSequence =
        """
        CREATE SEQUENCE IF NOT EXISTS public.entity_type_id_entitytypeid_seq;
        """;

    /// <summary>Creates the event stream id table.</summary>
    internal const string CreateEventStreamIdTable =
        """
        CREATE TABLE IF NOT EXISTS public.event_stream_id (
            EventStreamId integer DEFAULT nextval('public.entity_type_id_entitytypeid_seq'::regclass) NOT NULL,
            EventStream varchar(900) NOT NULL,
            CurrentVersion bigint NOT NULL DEFAULT 0,
            CONSTRAINT entity_type_id_pkey PRIMARY KEY (EventStreamId),
            CONSTRAINT entity_type_id_entitytype_key UNIQUE (EventStream)
        );
        """;

    /// <summary>Adds stream versions and projector checkpoints.</summary>
    internal const string CreateEventStreamVersionAndProjectorCheckpointV3 =
        """
        ALTER TABLE event_log
            ADD COLUMN IF NOT EXISTS StreamVersion bigint;

        WITH ranked AS (
            SELECT EventVersion,
                   ROW_NUMBER() OVER (PARTITION BY EventStreamId ORDER BY EventVersion) AS CalculatedStreamVersion
            FROM event_log
            WHERE StreamVersion IS NULL
        )
        UPDATE event_log target
        SET StreamVersion = ranked.CalculatedStreamVersion
        FROM ranked
        WHERE target.EventVersion = ranked.EventVersion;

        ALTER TABLE event_log
            ALTER COLUMN StreamVersion SET NOT NULL;


        ALTER TABLE event_stream_id
            ADD COLUMN IF NOT EXISTS CurrentVersion bigint NOT NULL DEFAULT 0;

        UPDATE event_stream_id stream
        SET CurrentVersion = versions.MaxStreamVersion
        FROM (
            SELECT EventStreamId, COALESCE(MAX(StreamVersion), 0) AS MaxStreamVersion
            FROM event_log
            GROUP BY EventStreamId
        ) versions
        WHERE stream.EventStreamId = versions.EventStreamId
          AND stream.CurrentVersion < versions.MaxStreamVersion;

        ALTER TABLE event_projector_state
            ADD COLUMN IF NOT EXISTS StreamVersion bigint;

        UPDATE event_projector_state eps
        SET StreamVersion = el.StreamVersion
        FROM event_log el
        WHERE eps.EventId = el.EventVersion
          AND eps.StreamVersion IS NULL;

        ALTER TABLE event_projector_state
            ALTER COLUMN StreamVersion SET NOT NULL;

        CREATE TABLE IF NOT EXISTS event_projector_stream_checkpoint (
            ProjectorName varchar(255) NOT NULL,
            EventStreamId bigint NOT NULL,
            LastAppliedStreamVersion bigint NOT NULL,
            LastAppliedEventId bigint NOT NULL,
            Revision bigint NOT NULL DEFAULT 0,
            UpdatedAtUtc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT pk_event_projector_stream_checkpoint PRIMARY KEY (ProjectorName, EventStreamId),
            CONSTRAINT fk_event_projector_stream_checkpoint_stream FOREIGN KEY (EventStreamId) REFERENCES event_stream_id(EventStreamId) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_event_projector_checkpoint_event_v3
            ON event_projector_stream_checkpoint (ProjectorName, LastAppliedEventId);

        CREATE INDEX IF NOT EXISTS ix_event_projector_state_stream_version_v3
            ON event_projector_state (ProjectorName, EventStreamId, StreamVersion);
        """;

    /// <summary>Creates the event version sequence.</summary>
    internal const string CreateEventVersionSequence =
        """
        CREATE SEQUENCE IF NOT EXISTS public.event_log_eventversion_seq;
        """;

    /// <summary>Creates the risk history journal.</summary>
    internal const string CreateRiskHistoryJournal =
        """
        CREATE TABLE IF NOT EXISTS risk_history_projection_progress (
            projection_name text PRIMARY KEY,
            after_event_version bigint NOT NULL CHECK(after_event_version>=0)
        );
        CREATE TABLE IF NOT EXISTS risk_history_projection_receipt (
            projection_name text NOT NULL,
            event_id bigint NOT NULL,
            projected_at_utc timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY(projection_name,event_id)
        );
        CREATE TABLE IF NOT EXISTS risk_history_projection_issue (
            projection_name text NOT NULL,
            event_id bigint NOT NULL,
            workflow_id uuid NOT NULL,
            invocation_id uuid NOT NULL,
            source_revision bigint NOT NULL,
            issue_code text NOT NULL,
            stored_hash text NOT NULL,
            incoming_hash text NOT NULL,
            detected_at_utc timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY(projection_name,event_id)
        );
        """;

    /// <summary>Covering lookup for compatible snapshot types in one stream; avoids reading unrelated event types or payload pages.</summary>
    internal const string CreateSnapshotLookupIndex =
        """
        CREATE INDEX IF NOT EXISTS ix_event_log_stream_name_version
        ON public.event_log (EventStreamId, EventNameId, EventVersion DESC);
        """;

    /// <summary>Drops the aggregate checkpoint.</summary>
    internal const string DropAggregateCheckpoint =
        """
        DROP TABLE IF EXISTS public.event_aggregate_checkpoint;
        """;

    /// <summary>Drops the business subscription journal.</summary>
    internal const string DropBusinessSubscriptionJournal =
        """
        DROP TABLE IF EXISTS business_subscription_projection_issue; DROP TABLE IF EXISTS business_subscription_projection_receipt;
        """;

    /// <summary>Drops the command log.</summary>
    internal const string DropCommandLog =
        """
        DROP TABLE IF EXISTS public.command_log;
        """;

    /// <summary>Drops the command log message pack payload.</summary>
    internal const string DropCommandLogMessagePackPayload =
        """
        ALTER TABLE command_log DROP CONSTRAINT IF EXISTS ck_command_log_payload_format;
        ALTER TABLE command_log
            DROP COLUMN IF EXISTS CommandPayloadSha256,
            DROP COLUMN IF EXISTS CommandPayloadVersion,
            DROP COLUMN IF EXISTS CommandPayloadFormat,
            DROP COLUMN IF EXISTS CommandPayload;
        """;

    /// <summary>Drops the event log table.</summary>
    internal const string DropEventLogTable =
        """
        DROP TABLE IF EXISTS public.event_log;
        """;

    /// <summary>Drops the event name id sequence.</summary>
    internal const string DropEventNameIdSequence =
        """
        DROP SEQUENCE IF EXISTS public.event_name_id_eventnameid_seq;
        """;

    /// <summary>Drops the event name id table.</summary>
    internal const string DropEventNameIdTable =
        """
        DROP TABLE IF EXISTS public.event_name_id;
        """;

    /// <summary>Drops the event projector outbox v2.</summary>
    internal const string DropEventProjectorOutboxV2 =
        """
        DROP TABLE IF EXISTS public.event_projector_outbox;
        """;

    /// <summary>Drops the event projector state.</summary>
    internal const string DropEventProjectorState =
        """
        DROP TABLE IF EXISTS public.event_projector_state;
        """;

    /// <summary>Drops the event projector state reliability v2.</summary>
    internal const string DropEventProjectorStateReliabilityV2 =
        """
        DROP INDEX IF EXISTS ix_event_projector_state_stream_pending_v2;
        DROP INDEX IF EXISTS ix_event_projector_state_pending_v2;
        ALTER TABLE IF EXISTS event_projector_state
            DROP COLUMN IF EXISTS UpdatedAtUtc,
            DROP COLUMN IF EXISTS BlockedStage,
            DROP COLUMN IF EXISTS LastCompletedStage,
            DROP COLUMN IF EXISTS BlockedReason,
            DROP COLUMN IF EXISTS LastErrorAtUtc,
            DROP COLUMN IF EXISTS NextAttemptAtUtc,
            DROP COLUMN IF EXISTS RetryCount,
            DROP COLUMN IF EXISTS LeaseExpiresAtUtc,
            DROP COLUMN IF EXISTS ExecutionToken,
            DROP COLUMN IF EXISTS Revision,
            DROP COLUMN IF EXISTS SourceEventName,
            DROP COLUMN IF EXISTS EventStreamId;
        """;

    /// <summary>Drops the event stream id sequence.</summary>
    internal const string DropEventStreamIdSequence =
        """
        DROP SEQUENCE IF EXISTS public.entity_type_id_entitytypeid_seq;
        """;

    /// <summary>Drops the event stream id table.</summary>
    internal const string DropEventStreamIdTable =
        """
        DROP TABLE IF EXISTS public.event_stream_id;
        """;

    /// <summary>Drops the event stream version and projector checkpoint v3.</summary>
    internal const string DropEventStreamVersionAndProjectorCheckpointV3 =
        """
        DROP INDEX IF EXISTS ix_event_projector_state_stream_version_v3;
        DROP INDEX IF EXISTS ix_event_projector_checkpoint_event_v3;
        DROP TABLE IF EXISTS event_projector_stream_checkpoint;
        DROP INDEX IF EXISTS ux_event_log_stream_version_v3;
        ALTER TABLE IF EXISTS event_projector_state DROP COLUMN IF EXISTS StreamVersion;
        ALTER TABLE IF EXISTS event_log DROP COLUMN IF EXISTS StreamVersion;
        ALTER TABLE IF EXISTS event_stream_id DROP COLUMN IF EXISTS CurrentVersion;
        """;

    /// <summary>Drops the event version sequence.</summary>
    internal const string DropEventVersionSequence =
        """
        DROP SEQUENCE IF EXISTS public.event_log_eventversion_seq;
        """;

    /// <summary>Drops the message origin metadata.</summary>
    internal const string DropMessageOriginMetadata =
        """
        ALTER TABLE public.command_log DROP COLUMN IF EXISTS MessageOriginJson;
        ALTER TABLE public.event_projector_outbox DROP COLUMN IF EXISTS MessageOriginJson;
        ALTER TABLE public.event_log DROP COLUMN IF EXISTS MessageOriginJson;
        """;

    /// <summary>Drops the risk history journal.</summary>
    internal const string DropRiskHistoryJournal =
        """
        DROP TABLE IF EXISTS risk_history_projection_issue; DROP TABLE IF EXISTS risk_history_projection_receipt; DROP TABLE IF EXISTS risk_history_projection_progress;
        """;

    /// <summary>Drops the snapshot lookup index.</summary>
    internal const string DropSnapshotLookupIndex =
        """
        DROP INDEX IF EXISTS public.ix_event_log_stream_name_version;
        """;
}
