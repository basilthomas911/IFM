using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

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
        objects = EventSourceSchemaSql.GetDefinitions(options);
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => objects;
}
