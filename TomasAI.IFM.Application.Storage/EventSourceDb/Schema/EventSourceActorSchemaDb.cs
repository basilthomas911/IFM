using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.EventSourceDb.Schema.EventSourceActorSchemaSql;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Schema;

/// <summary>
/// Shared schema for EventSourceActorDbContext.
/// </summary>
public sealed class EventSourceSchemaDb
    : SchemaDbContext<EventSourceSchemaDb>
{
    readonly SchemaObjectDefinition[] objects;

    /// <summary>Initializes the complete event-store schema using the named PostgreSQL connection.</summary>
    /// <param name="connectionSettings">The named database connection settings.</param>
    /// <param name="logger">The schema provider logger.</param>
    /// <param name="eventLogPersistenceOptions">The optional event log persistence configuration.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "The database provider owns this injected logger; retain its category and constructor contract while the context supplies structured component names.")]
    public EventSourceSchemaDb(
        IDbConnectionSettings connectionSettings,
        ILogger<DbProvider> logger,
        EventLogPersistenceOptions? eventLogPersistenceOptions = null)
        : base(connectionSettings[EventSourceActorDbContext.EventSourceActorDbConnection], logger)
    {
        var options = (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate();
        objects = GetDefinitions(options);
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => objects;

    /// <summary>Upgrades an existing event store before aggregate checkpoint readers start.</summary>
    public Task EnsureAggregateCheckpointSchemaAsync(CancellationToken cancellationToken = default) =>
        CreateAsync([AggregateCheckpointSchemaObject], cancellationToken);

    const string AggregateCheckpointSchemaObject = "event_aggregate_checkpoint_v1";

    /// <summary>Returns event-store objects in dependency order for the configured log layout.</summary>
    internal static SchemaObjectDefinition[] GetDefinitions(EventLogPersistenceOptions options) =>
    [
        new("entity_type_id_entitytypeid_seq", CreateEventStreamIdSequence, DropEventStreamIdSequence),
        new("event_name_id_eventnameid_seq", CreateEventNameIdSequence, DropEventNameIdSequence),
        new("event_log_eventversion_seq", CreateEventVersionSequence, DropEventVersionSequence),
        new("event_stream_id", CreateEventStreamIdTable, DropEventStreamIdTable),
        new("event_name_id", CreateEventNameIdTable, DropEventNameIdTable),
        new("event_log", CreateEventLogTable, DropEventLogTable),
        new("event_log_snapshot_lookup_v4", CreateSnapshotLookupIndex, DropSnapshotLookupIndex),
        new(AggregateCheckpointSchemaObject, CreateAggregateCheckpoint, DropAggregateCheckpoint),
        new("command_log", CreateCommandLog, DropCommandLog),
        new("command_log_messagepack_v1", AddCommandLogMessagePackPayload, DropCommandLogMessagePackPayload),
        new("event_projector_state", CreateEventProjectorState, DropEventProjectorState),
        new("business_subscription_projection_receipt", EventLogSqlLayout.ForProduction(options).Resolve(CreateBusinessSubscriptionJournal), DropBusinessSubscriptionJournal),
        new("risk_history_projection_progress", CreateRiskHistoryJournal, DropRiskHistoryJournal),
        new("event_projector_state_reliability_v2", CreateEventProjectorStateReliabilityV2, DropEventProjectorStateReliabilityV2),
        new("event_projector_outbox_v2", CreateEventProjectorOutboxV2, DropEventProjectorOutboxV2),
        new("event_stream_version_projector_checkpoint_v3", CreateEventStreamVersionAndProjectorCheckpointV3, DropEventStreamVersionAndProjectorCheckpointV3),
        new("message_origin_metadata_v1", AddMessageOriginMetadata, DropMessageOriginMetadata),
    ];
}
