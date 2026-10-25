using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
/// <summary>Creates every ConfigurationDb PostgreSQL schema object, including tables, indexes, constraints, functions, triggers and seed definitions.</summary>
public sealed class ConfigurationSchemaDb : SchemaDbContext<ConfigurationSchemaDb>
{
    /// <summary>Initializes the complete schema catalog with lookup defaults.</summary>
    /// <param name = "connectionSettings">The named PostgreSQL connection settings.</param>
    /// <param name = "logger">The schema-provider logger.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6672", Justification = "SchemaDbContext shares the storage provider logger category.")]
    public ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : this(connectionSettings ?? throw new ArgumentNullException(nameof(connectionSettings)), logger, true)
    {
    }

    /// <summary>Initializes the complete schema catalog without lookup default rows for isolated rebuilds or offline migration.</summary>
    /// <param name = "connectionSettings">The named PostgreSQL connection settings.</param>
    /// <param name = "logger">The schema-provider logger.</param>
    /// <returns>The schema executor with lookup default insertion disabled.</returns>
    public static ConfigurationSchemaDb WithoutLookupDefaults(IDbConnectionSettings connectionSettings, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connectionSettings);
        return new(connectionSettings, logger as ILogger<DbProvider> ?? new ProviderCategoryLogger(logger), false);
    }

    // Keep provider logging details inside the Application implementation.
    sealed class ProviderCategoryLogger(ILogger inner) : ILogger<DbProvider>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel level) => inner.IsEnabled(level);
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(level, id, state, exception, formatter);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6672", Justification = "SchemaDbContext shares the storage provider logger category.")]
    private ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger, bool seedLookupDefaults) : base(connectionSettings[ConfigurationDbContext.ConfigurationDbConnection], logger)
    {
        definitions = ConfigurationSchemaSql.GetDefinitions(seedLookupDefaults);
    }

    readonly SchemaObjectDefinition[] definitions;
    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => definitions;
}
