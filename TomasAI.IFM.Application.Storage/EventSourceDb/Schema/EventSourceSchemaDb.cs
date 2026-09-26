using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Schema;

/// <summary>
/// Shared schema for EventSourceActorDbContext.
/// </summary>
public sealed class EventSourceSchemaDb
    : SchemaDbContext<EventSourceSchemaDb>
{
    readonly SchemaObjectDefinition[] objects;

    public EventSourceSchemaDb(
        IDbConnectionSettings connectionSettings,
        ILogger<DbProvider> logger,
        EventLogPersistenceOptions? eventLogPersistenceOptions = null)
        : base(connectionSettings[EventSourceActorDbContext.EventSourceActorDbConnection], logger)
    {
        var options = (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate();
        var projectorState = EventSourceSchemaSql.CreateEventProjectorState.Replace("""
                CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_event_version
                ON event_log (EventVersion);

                """, string.Empty, StringComparison.Ordinal);
        var streamVersion = EventSourceSchemaSql.CreateEventStreamVersionAndProjectorCheckpointV3.Replace("""
                CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_stream_version_v3
                    ON event_log (EventStreamId, StreamVersion);

                """, string.Empty, StringComparison.Ordinal);
        var definitions = new List<SchemaObjectDefinition>
        {
            new("entity_type_id_entitytypeid_seq", EventSourceSchemaSql.CreateEventStreamIdSequence, "DROP SEQUENCE IF EXISTS public.entity_type_id_entitytypeid_seq;"),
            new("event_name_id_eventnameid_seq", EventSourceSchemaSql.CreateEventNameIdSequence, "DROP SEQUENCE IF EXISTS public.event_name_id_eventnameid_seq;"),
            new("event_log_eventversion_seq", EventSourceSchemaSql.CreateEventVersionSequence, "DROP SEQUENCE IF EXISTS public.event_log_eventversion_seq;"),
            new("event_stream_id", EventSourceSchemaSql.CreateEventStreamIdTable, "DROP TABLE IF EXISTS public.event_stream_id;"),
            new("event_name_id", EventSourceSchemaSql.CreateEventNameIdTable, "DROP TABLE IF EXISTS public.event_name_id;"),
            new("event_log", EventSourceSchemaSql.CreateEventLogTable, "DROP TABLE IF EXISTS public.event_log;")
        };
        definitions.AddRange([
            new("command_log", EventSourceSchemaSql.CreateCommandLog, "DROP TABLE IF EXISTS public.command_log;"),
            new("command_log_messagepack_v1", EventSourceSchemaSql.AddCommandLogMessagePackPayload,
                EventSourceSchemaSql.DropCommandLogMessagePackPayload),
            new("event_projector_state", projectorState, "DROP TABLE IF EXISTS public.event_projector_state;"),
            new("business_subscription_projection_receipt", PostgresCommittedBusinessEventJournal.CreateTableFor(options),
                "DROP TABLE IF EXISTS business_subscription_projection_receipt;"),
            new("risk_history_projection_progress", RiskHistoryJournal.CreateTables,
                "DROP TABLE IF EXISTS risk_history_projection_issue; DROP TABLE IF EXISTS risk_history_projection_receipt; DROP TABLE IF EXISTS risk_history_projection_progress;"),
            new("event_projector_state_reliability_v2", EventSourceSchemaSql.CreateEventProjectorStateReliabilityV2, EventSourceSchemaSql.DropEventProjectorStateReliabilityV2),
            new("event_projector_outbox_v2", EventSourceSchemaSql.CreateEventProjectorOutboxV2, "DROP TABLE IF EXISTS public.event_projector_outbox;"),
            new("event_stream_version_projector_checkpoint_v3", streamVersion, EventSourceSchemaSql.DropEventStreamVersionAndProjectorCheckpointV3),
            new("historical_data_loader", EventSourceSchemaSql.CreateHistoricalDataLoader,
                "DROP TABLE IF EXISTS historical_data_load_manifest; DROP TABLE IF EXISTS historical_data_load_checkpoint;")
        ]);
        objects = [.. definitions];
    }

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => objects;
}
