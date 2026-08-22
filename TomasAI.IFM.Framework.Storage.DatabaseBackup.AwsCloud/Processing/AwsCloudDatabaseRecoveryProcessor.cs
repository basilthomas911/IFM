using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Application.DatabaseBackup.Processing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

public sealed class AwsCloudDatabaseRecoveryProcessor
    : IDatabaseRecoveryProcessor, IDatabaseRecoveryOperationExecutor, IDatabaseRecoveryProcessorRouting
{
    readonly AwsCloudDatabaseBackupOptions _options;
    readonly AwsDatabaseRecoveryEngineSelector _selector;
    readonly DatabaseRecoveryOperationOrchestrator _orchestrator;

    public AwsCloudDatabaseRecoveryProcessor(
        DynamoDbDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        IScyllaBackupCapability scylla,
        S3DatabaseBackupPublicationCapability publication,
        S3DatabaseRestoreSourceCapability restoreSources,
        AwsRecoveryEvidenceStore evidence,
        S3DatabaseBackupCatalog catalog,
        AwsDatabaseRecoveryEngineSelector selector,
        AwsCloudDatabaseBackupOptions options,
        DatabaseBackupHostOptions hostOptions)
    {
        _options = options;
        _selector = selector;
        var chainPlanner = new DatabaseBackupChainPlanner(catalog,
            new DatabaseBackupChainPolicy(true, options.MaximumIncrementalChainDepth,
                TimeSpan.FromDays(options.MaximumBaseAgeDays)));
        _orchestrator = new DatabaseRecoveryOperationOrchestrator(
            BackupSource.AwsCloud, journal, postgreSql, scylla, selector, hostOptions,
            publication, restoreSources, evidence, chainPlanner);
    }

    public BackupSource Source => BackupSource.AwsCloud;
    public bool CanProcess(DatabaseProtectionSetId protectionSetId) => _selector.CanSelect(protectionSetId);

    public ValueTask<DatabaseExecutionAdmission> AdmitAsync(
        DatabaseExecutionIntent intent, CancellationToken cancellationToken)
    {
        if (!_options.AcceptBackupRequests) throw new AwsCloudRequestAdmissionDisabledException();
        return _orchestrator.AdmitAsync(intent, cancellationToken);
    }

    public ValueTask ExecuteAsync(RecoverableJournalOperation operation, CancellationToken cancellationToken)
        => _orchestrator.ExecuteAsync(operation, cancellationToken);
}

public sealed class AwsCloudRequestAdmissionDisabledException()
    : InvalidOperationException("AWS cloud backup request admission is disabled by configuration.");
