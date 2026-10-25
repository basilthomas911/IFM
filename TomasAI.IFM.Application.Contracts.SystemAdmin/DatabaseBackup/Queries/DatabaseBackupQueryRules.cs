using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;

/// <summary>Validates direct query contracts without inherited wire fields or adapters.</summary>
public static class DatabaseBackupQueryRules
{
    /// <summary>Validates common fields and query-specific requirements.</summary>
    public static void Validate(IDatabaseBackupQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Request.Validate();
        if (query.EntityId.Value == Guid.Empty) throw new ArgumentException("Query entity ID is required.",nameof(query));
        if (query.Source != BackupSource.None) DatabaseBackupEnumValidation.RequireConcrete(query.Source);
        if (query.PageSize is < 1 or > DatabaseBackupContractLimits.MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(query));
        if (query.ContinuationIdentity.Length > DatabaseBackupContractLimits.IdentifierLength || query.ContinuationIdentity.Any(char.IsControl))
            throw new ArgumentOutOfRangeException(nameof(query));
        if (query.FromUtc.HasValue && query.FromUtc.Value.Offset != TimeSpan.Zero) throw new ArgumentException("FromUtc must be UTC.",nameof(query));
        if (query.ToUtc.HasValue && query.ToUtc.Value.Offset != TimeSpan.Zero) throw new ArgumentException("ToUtc must be UTC.",nameof(query));
        switch (query)
        {
            case GetDatabaseBackupLogQuery or GetDatabaseBackupOperationQuery or GetDatabaseRestoreOperationQuery or GetDatabaseRecoveryRunStatsQuery:
                if (query.OperationId is null || query.OperationId.Value.Value == Guid.Empty) throw new ArgumentException("Operation ID is required.",nameof(query));
                break;
            case GetDatabaseBackupPolicyQuery:
                if (query.PolicyId is null) throw new ArgumentException("Policy ID is required.",nameof(query));
                break;
            case GetDatabaseBackupSetQuery:
                if (query.BackupSetId is null) throw new ArgumentException("Backup set ID is required.",nameof(query));
                break;
            case GetDatabaseRestorePointQuery:
                DatabaseBackupEnumValidation.RequireConcrete(query.Source);
                if (query.RestorePointId is null) throw new ArgumentException("Restore point ID is required.",nameof(query));
                break;
            case GetLatestVerifiedDatabaseBackupQuery or GetLatestRestoreTestedDatabaseBackupQuery:
                DatabaseBackupEnumValidation.RequireConcrete(query.Source);
                if (query.ProtectionSetId is null) throw new ArgumentException("Protection set is required.",nameof(query));
                break;
            case GetDatabaseRetentionForecastQuery:
                DatabaseBackupEnumValidation.RequireConcrete(query.Source);
                break;
        }
    }
}
