using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Schema;

/// <summary>
/// Shared schema for EventSourceActorDbContext.
/// </summary>
public sealed class EventSourceSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<EventSourceSchemaDb>(connectionSettings[EventSourceActorDbContext.EventSourceActorDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("entity_type_id_entitytypeid_seq", EventSourceSchemaSql.CreateEventStreamIdSequence, "DROP SEQUENCE IF EXISTS public.entity_type_id_entitytypeid_seq;"),
        new("event_name_id_eventnameid_seq", EventSourceSchemaSql.CreateEventNameIdSequence, "DROP SEQUENCE IF EXISTS public.event_name_id_eventnameid_seq;"),
        new("event_log_eventversion_seq", EventSourceSchemaSql.CreateEventVersionSequence, "DROP SEQUENCE IF EXISTS public.event_log_eventversion_seq;"),
        new("event_stream_id", EventSourceSchemaSql.CreateEventStreamIdTable, "DROP TABLE IF EXISTS public.event_stream_id;"),
        new("event_name_id", EventSourceSchemaSql.CreateEventNameIdTable, "DROP TABLE IF EXISTS public.event_name_id;"),
        new("event_log", EventSourceSchemaSql.CreateEventLogTable, "DROP TABLE IF EXISTS public.event_log;"),
        new("command_log", EventSourceSchemaSql.CreateCommandLog, "DROP TABLE IF EXISTS public.command_log;"),
        new("event_projector_state", EventSourceSchemaSql.CreateEventProjectorState, "DROP TABLE IF EXISTS public.event_projector_state;"),
        new("event_projector_state_reliability_v2", EventSourceSchemaSql.CreateEventProjectorStateReliabilityV2, EventSourceSchemaSql.DropEventProjectorStateReliabilityV2),
        new("event_projector_outbox_v2", EventSourceSchemaSql.CreateEventProjectorOutboxV2, "DROP TABLE IF EXISTS public.event_projector_outbox;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
