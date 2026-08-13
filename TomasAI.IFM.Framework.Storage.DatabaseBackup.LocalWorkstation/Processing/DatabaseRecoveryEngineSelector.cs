using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

public interface IDatabaseRecoveryEngineSelector
{
    DatabaseEngine Select(DatabaseProtectionSetId protectionSetId);
    bool CanSelect(DatabaseProtectionSetId protectionSetId);
}

public sealed class LocalWorkstationDatabaseRecoveryEngineSelector : IDatabaseRecoveryEngineSelector
{
    readonly HashSet<string> _postgreSql;
    readonly HashSet<string> _scylla;

    public LocalWorkstationDatabaseRecoveryEngineSelector(
        PostgreSqlBackupOptions postgreSql,
        ScyllaBackupOptions scylla,
        LocalWorkstationSourceOptions source)
    {
        ArgumentNullException.ThrowIfNull(postgreSql);
        ArgumentNullException.ThrowIfNull(scylla);
        ArgumentNullException.ThrowIfNull(source);
        _postgreSql = source.IsEngineEnabled(DatabaseEngine.PostgreSql)
            ? postgreSql.AllowedProtectionSets.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        _scylla = source.IsEngineEnabled(DatabaseEngine.ScyllaDb)
            ? scylla.ProtectionSets.Keys.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var duplicate = _postgreSql.Intersect(_scylla, StringComparer.Ordinal).FirstOrDefault();
        if (duplicate is not null)
            throw new InvalidOperationException("A database protection set cannot select both PostgreSQL and Scylla.");
    }

    public DatabaseEngine Select(DatabaseProtectionSetId protectionSetId)
    {
        if (_postgreSql.Contains(protectionSetId.Value)) return DatabaseEngine.PostgreSql;
        if (_scylla.Contains(protectionSetId.Value)) return DatabaseEngine.ScyllaDb;
        throw new InvalidOperationException("The database protection set is not mapped to an allowlisted native engine.");
    }

    public bool CanSelect(DatabaseProtectionSetId protectionSetId)
        => _postgreSql.Contains(protectionSetId.Value) || _scylla.Contains(protectionSetId.Value);
}

internal sealed class PostgreSqlOnlyDatabaseRecoveryEngineSelector : IDatabaseRecoveryEngineSelector
{
    public DatabaseEngine Select(DatabaseProtectionSetId protectionSetId) => DatabaseEngine.PostgreSql;
    public bool CanSelect(DatabaseProtectionSetId protectionSetId) => true;
}
