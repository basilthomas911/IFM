namespace TomasAI.IFM.Application.Storage.EventSourceDb.Schema;

public static class EventSourceSchemaSql
{
    public const string CreateEventStreamIdSequence = "CREATE SEQUENCE IF NOT EXISTS public.entity_type_id_entitytypeid_seq;";
    public const string CreateEventNameIdSequence = "CREATE SEQUENCE IF NOT EXISTS public.event_name_id_eventnameid_seq;";
    public const string CreateEventVersionSequence = "CREATE SEQUENCE IF NOT EXISTS public.event_log_eventversion_seq;";

    public const string CreateEventStreamIdTable = """
        CREATE TABLE IF NOT EXISTS public.event_stream_id (
            EventStreamId integer DEFAULT nextval('public.entity_type_id_entitytypeid_seq'::regclass) NOT NULL,
            EventStream varchar(900) NOT NULL,
            CurrentVersion bigint NOT NULL DEFAULT 0,
            CONSTRAINT entity_type_id_pkey PRIMARY KEY (EventStreamId),
            CONSTRAINT entity_type_id_entitytype_key UNIQUE (EventStream)
        );
        """;

    public const string CreateEventNameIdTable = """
        CREATE TABLE IF NOT EXISTS public.event_name_id (
            EventNameId integer DEFAULT nextval('public.event_name_id_eventnameid_seq'::regclass) NOT NULL,
            EventName varchar(64) NOT NULL,
            EventTypeName varchar(512) NOT NULL,
            CONSTRAINT event_name_id_pkey PRIMARY KEY (EventNameId),
            CONSTRAINT event_name_id_eventname_fulleventname_key UNIQUE (EventName, EventTypeName)
        );
        """;

    public const string CreateEventLogTable = """
        CREATE TABLE IF NOT EXISTS public.event_log (
            EventStreamId bigint NOT NULL,
            EventNameId integer NOT NULL,
            EventVersion bigint DEFAULT nextval('public.event_log_eventversion_seq'::regclass) NOT NULL,
            StreamVersion bigint NOT NULL,
            EventData text NOT NULL,
            CommandId uuid NOT NULL,
            EventTimestamp text NOT NULL,
            CONSTRAINT event_log_pkey PRIMARY KEY (EventStreamId, EventNameId, EventVersion)
        );

        CREATE INDEX IF NOT EXISTS ix_event_log_command_id
        ON public.event_log (CommandId);
        """;

    public const string CreateEventProjectorState = """
    CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_event_version
    ON event_log (EventVersion);

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
    CONSTRAINT fk_event_projector_state_event_log
    FOREIGN KEY (EventId) REFERENCES event_log(EventVersion) ON DELETE CASCADE
    );

    CREATE INDEX IF NOT EXISTS ix_event_projector_state_projector_outcome
    ON event_projector_state (ProjectorName, Outcome);
    """;

    public const string CreateEventProjectorStateReliabilityV2 = """
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

    public const string DropEventProjectorStateReliabilityV2 = """
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

    public const string CreateEventProjectorOutboxV2 = """
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
        CONSTRAINT fk_event_projector_outbox_state
            FOREIGN KEY (EventId, ProjectorName)
            REFERENCES event_projector_state(EventId, ProjectorName)
            ON DELETE CASCADE,
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
    """;

    public const string CreateEventStreamVersionAndProjectorCheckpointV3 = """
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

    CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_stream_version_v3
        ON event_log (EventStreamId, StreamVersion);

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
        CONSTRAINT pk_event_projector_stream_checkpoint
            PRIMARY KEY (ProjectorName, EventStreamId),
        CONSTRAINT fk_event_projector_stream_checkpoint_stream
            FOREIGN KEY (EventStreamId) REFERENCES event_stream_id(EventStreamId) ON DELETE CASCADE
    );

    CREATE INDEX IF NOT EXISTS ix_event_projector_checkpoint_event_v3
        ON event_projector_stream_checkpoint (ProjectorName, LastAppliedEventId);

    CREATE INDEX IF NOT EXISTS ix_event_projector_state_stream_version_v3
        ON event_projector_state (ProjectorName, EventStreamId, StreamVersion);
    """;

    public const string DropEventStreamVersionAndProjectorCheckpointV3 = """
    DROP INDEX IF EXISTS ix_event_projector_state_stream_version_v3;
    DROP INDEX IF EXISTS ix_event_projector_checkpoint_event_v3;
    DROP TABLE IF EXISTS event_projector_stream_checkpoint;
    DROP INDEX IF EXISTS ux_event_log_stream_version_v3;
    ALTER TABLE IF EXISTS event_projector_state DROP COLUMN IF EXISTS StreamVersion;
    ALTER TABLE IF EXISTS event_log DROP COLUMN IF EXISTS StreamVersion;
    ALTER TABLE IF EXISTS event_stream_id DROP COLUMN IF EXISTS CurrentVersion;
    """;

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
}
