using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

public sealed class AwsDatabaseRecoveryEngineSelector : IDatabaseRecoveryEngineSelector
{
    readonly HashSet<string> _postgreSql;
    readonly HashSet<string> _scylla;

    public AwsDatabaseRecoveryEngineSelector(AwsCloudDatabaseBackupOptions options)
    {
        options.Validate();
        _postgreSql = options.PostgreSqlProtectionSets.ToHashSet(StringComparer.Ordinal);
        _scylla = options.ScyllaProtectionSets.ToHashSet(StringComparer.Ordinal);
    }

    public DatabaseEngine Select(DatabaseProtectionSetId protectionSetId)
    {
        if (_postgreSql.Contains(protectionSetId.Value)) return DatabaseEngine.PostgreSql;
        if (_scylla.Contains(protectionSetId.Value)) return DatabaseEngine.ScyllaDb;
        throw new InvalidOperationException("The AWS protection set is not mapped to an allowlisted native engine.");
    }

    public bool CanSelect(DatabaseProtectionSetId protectionSetId)
        => _postgreSql.Contains(protectionSetId.Value) || _scylla.Contains(protectionSetId.Value);
}
