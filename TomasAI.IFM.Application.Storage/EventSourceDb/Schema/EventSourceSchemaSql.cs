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
