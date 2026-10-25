using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SchemaDb;

/// <summary>
/// Executes checked-in schema definitions against an existing database or keyspace.
/// </summary>
public abstract class SchemaDbContext<TSchemaDb>(
    IDbConnectionSetting connectionSetting,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<TSchemaDb>(connectionSetting, logger), IDbSchemaContext
    where TSchemaDb : IObjectRepository
{
    protected abstract IReadOnlyList<SchemaObjectDefinition> Definitions { get; }

    public override IObjectRepository Database => this;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2365", Justification = "IDbSchemaContext requires this metadata property; it returns a defensive name snapshot rather than exposing mutable schema definitions.")]
    public IReadOnlyList<string> ManagedObjects => Definitions.Select(definition => definition.Name).ToArray();

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2365", Justification = "Schema metadata returns a defensive snapshot of fully resolved table names, including generated definitions.")]
    public IReadOnlyList<string> ManagedTables => Definitions.SelectMany(definition =>
            System.Text.RegularExpressions.Regex.Matches(definition.CreateStatement,
                @"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<table>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)\s*\(",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.NonBacktracking)
                .Select(match => match.Groups["table"].Value))
            .Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Queries each checked-in table through the configured application provider. This is
    /// read-only: no schema migrations, seed data, sequence allocations or actors are run.
    /// Table readability is a recovery gate, not proof of restored data completeness.
    /// </summary>
    public virtual async Task<int> ValidateTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = ManagedTables;
        if (tables.Count == 0)
            throw new InvalidOperationException($"{GetType().Name} has no readable table validation definitions.");
        foreach (var table in tables)
        {
            using var query = Use($"Recovery.Validate.{table}", $"SELECT * FROM {table} LIMIT 1;");
            _ = await query.ExecuteQueryAsync(static _ => true, cancellationToken).ConfigureAwait(false);
        }
        return tables.Count;
    }

    /// <summary>
    /// Creates only the named schema objects, preserving their declared dependency order.
    /// This is intended for additive, narrowly scoped operator migrations.
    /// </summary>
    public async Task CreateAsync(
        IReadOnlyCollection<string> objectNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectNames);

        var requested = objectNames.ToHashSet(StringComparer.Ordinal);
        if (requested.Count != objectNames.Count)
            throw new ArgumentException("Schema object names must be unique.", nameof(objectNames));

        var known = Definitions.Select(static definition => definition.Name).ToHashSet(StringComparer.Ordinal);
        var unknown = requested.Except(known, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length != 0)
        {
            throw new ArgumentException(
                $"Unknown managed schema object(s): {string.Join(", ", unknown)}.",
                nameof(objectNames));
        }

        foreach (var definition in Definitions)
        {
            if (!requested.Contains(definition.Name))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await Use(
                        $"{GetType().Name}.Create.{definition.Name}",
                        definition.CreateStatement)
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IsAlreadyApplied(definition, exception))
            {
                // Some supported Scylla releases do not implement ADD IF NOT EXISTS.
                // Checked-in additive migrations therefore identify only the exact
                // provider messages that mean the requested schema is already present.
            }
        }
    }

    static bool IsAlreadyApplied(SchemaObjectDefinition definition, Exception exception)
    {
        var fragments = definition.AlreadyAppliedErrorFragments;
        if (fragments is null || fragments.Count == 0)
            return false;
        var error = exception.ToString();
        return fragments.Any(fragment =>
            error.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreateAllAsync()
        => await CreateAsync(ManagedObjects).ConfigureAwait(false);

    /// <summary>
    /// Destructively recreates only the named objects. Callers must explicitly name
    /// every disposable object; dependencies are dropped in reverse declaration order
    /// and recreated in declaration order.
    /// </summary>
    public async Task RecreateAsync(
        IReadOnlyCollection<string> objectNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectNames);
        var requested = objectNames.ToHashSet(StringComparer.Ordinal);
        if (requested.Count != objectNames.Count)
            throw new ArgumentException("Schema object names must be unique.", nameof(objectNames));

        var known = Definitions.Select(static definition => definition.Name)
            .ToHashSet(StringComparer.Ordinal);
        var unknown = requested.Except(known, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length != 0)
            throw new ArgumentException(
                $"Unknown managed schema object(s): {string.Join(", ", unknown)}.",
                nameof(objectNames));

        foreach (var definition in Definitions.Reverse())
        {
            if (!requested.Contains(definition.Name))
                continue;
            cancellationToken.ThrowIfCancellationRequested();
            await Use(
                    $"{GetType().Name}.Drop.{definition.Name}",
                    definition.DropStatement)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        await CreateAsync(objectNames, cancellationToken).ConfigureAwait(false);
    }

    public async Task DropAllAsync()
    {
        foreach (var definition in Definitions.Reverse())
            await Use(
                    $"{GetType().Name}.Drop.{definition.Name}",
                    definition.DropStatement)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);
    }
}
